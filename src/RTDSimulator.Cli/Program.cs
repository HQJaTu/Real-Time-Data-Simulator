using System.Diagnostics;
using RTDSimulator.Core;
using RTDSimulator.EventHubs;
using RTDSimulator.Kusto;
using RTDSimulator.ServiceBus;

namespace RTDSimulator.Cli;

internal static class Program
{
    private const double BytesToMbps = 1024 * 1024 / 8;

    /// <summary>Available connectors. Add new connectors here.</summary>
    private static readonly IReadOnlyList<ITargetConnector> Connectors = new ITargetConnector[]
    {
        new EventHubsConnector(),
        new ServiceBusConnector(),
        new KustoStreamingConnector(),
        new KustoQueuedConnector(),
    };

    private static async Task<int> Main(string[] args)
    {
        var options = CliOptions.Parse(args, out string? error);
        if (options is null)
        {
            if (error is not null)
            {
                Console.Error.WriteLine($"Error: {error}");
                Console.Error.WriteLine();
            }
            PrintUsage();
            return error is null ? 0 : 1;
        }

        ITargetConnector? connector = Connectors.FirstOrDefault(
            c => string.Equals(c.Key, options.TargetKey, StringComparison.OrdinalIgnoreCase));
        if (connector is null)
        {
            Console.Error.WriteLine($"Error: unknown --target '{options.TargetKey}'. Known targets: {string.Join(", ", Connectors.Select(c => c.Key))}.");
            return 1;
        }

        if (!ValidateParameters(connector, options.Parameters, out string? paramError))
        {
            Console.Error.WriteLine($"Error: {paramError}");
            Console.Error.WriteLine();
            PrintConnectorParameters(connector);
            return 1;
        }

        string payload;
        try
        {
            payload = await File.ReadAllTextAsync(options.TemplatePath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: could not read template '{options.TemplatePath}': {ex.Message}");
            return 1;
        }

        IReadOnlyList<VariableDefinition> variables;
        try
        {
            variables = ResolveVariables(options.VariablesPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: could not load variable definitions: {ex.Message}");
            return 1;
        }

        long totalBatches = (long)options.Parallelism * options.BatchesPerSender;
        long totalMessages = totalBatches * options.MessagesPerBatch;
        Console.WriteLine($"Target     : {connector.DisplayName}");
        foreach (ConnectionParameter p in connector.Parameters)
        {
            string value = options.Parameters.GetValueOrDefault(p.Key, "");
            Console.WriteLine($"{p.Label,-12}: {(p.Secret ? Mask(value) : value)}");
        }
        Console.WriteLine($"Parallelism: {options.Parallelism}");
        Console.WriteLine($"Batches    : {options.BatchesPerSender} per sender ({totalBatches} total)");
        Console.WriteLine($"Messages   : {options.MessagesPerBatch} per batch ({totalMessages} total)");
        Console.WriteLine($"Wait time  : {options.WaitTimeSeconds}s between batches");
        Console.WriteLine();

        // Sender clients are thread-safe, so a single connection is shared across all
        // senders. (The CLI authenticates via the ambient Azure credential chain
        // for connectors that require Azure Identity, e.g. Kusto.)
        await using ITargetConnection connection = connector.CreateConnection(options.Parameters);

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Console.WriteLine();
            Console.WriteLine("Cancellation requested, stopping...");
            cts.Cancel();
        };

        long batchesSent = 0;
        long messagesSent = 0;
        long bytesSent = 0;
        DateTime runStartUtc = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        var generator = new LoadGenerator(new PayloadGenerator(payload, variables))
        {
            BatchesNo = options.BatchesPerSender,
            EventsPerBatch = options.MessagesPerBatch,
            WaitTime = TimeSpan.FromSeconds(options.WaitTimeSeconds),
        };

        generator.BatchSent += (_, e) =>
        {
            long sent = Interlocked.Increment(ref batchesSent);
            Interlocked.Add(ref messagesSent, e.MessageCount);
            Interlocked.Add(ref bytesSent, e.SizeInBytes);

            double seconds = stopwatch.Elapsed.TotalSeconds;
            double tps = seconds > 0 ? messagesSent / seconds : 0;
            double mbps = seconds > 0 ? bytesSent / BytesToMbps / seconds : 0;
            Console.Write($"\rBatches: {sent}/{totalBatches} | Messages: {messagesSent} | TPS: {tps:F2} | {bytesSent:0,0} bytes ({mbps:F2} Mbps)   ");
        };

        int exitCode = 0;
        try
        {
            await generator.Send(connection, options.Parallelism, cts.Token);
        }
        catch (Exception ex)
        {
            exitCode = 1;
            Console.Error.WriteLine();
            Console.Error.WriteLine($"Error during send: {ex.Message}");
        }

        stopwatch.Stop();
        Console.WriteLine();
        Console.WriteLine();
        string status = cts.IsCancellationRequested ? "Cancelled" : exitCode == 0 ? "Completed" : "Failed";
        double totalSeconds = stopwatch.Elapsed.TotalSeconds;
        double finalTps = totalSeconds > 0 ? messagesSent / totalSeconds : 0;
        double finalMbps = totalSeconds > 0 ? bytesSent / BytesToMbps / totalSeconds : 0;
        Console.WriteLine($"{status}: sent {messagesSent} messages in {batchesSent} batches.");
        Console.WriteLine($"Total time: {stopwatch.Elapsed:c} | TPS: {finalTps:F2} | {bytesSent:0,0} bytes ({finalMbps:F2} Mbps)");

        if (options.VerifyIngestion && connection is IIngestionVerifier verifier)
        {
            exitCode = await VerifyIngestionAsync(verifier, runStartUtc, exitCode);
        }

        return exitCode;
    }

    private static async Task<int> VerifyIngestionAsync(IIngestionVerifier verifier, DateTime sinceUtc, int exitCode)
    {
        Console.WriteLine();
        Console.WriteLine("Verifying ingestion (querying '.show ingestion failures')...");
        try
        {
            IReadOnlyList<string> failures = await verifier.GetIngestionFailuresSinceAsync(sinceUtc);
            if (failures.Count == 0)
            {
                Console.WriteLine("No ingestion failures reported. Note: queued failures can lag — re-run the check later if in doubt.");
                return exitCode;
            }

            Console.Error.WriteLine($"{failures.Count} ingestion failure(s) reported:");
            foreach (string failure in failures)
                Console.Error.WriteLine($"  {failure}");
            return exitCode == 0 ? 1 : exitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not query ingestion failures: {ex.Message}");
            return exitCode;
        }
    }

    private static bool ValidateParameters(ITargetConnector connector, IReadOnlyDictionary<string, string> values, out string? error)
    {
        var knownKeys = connector.Parameters.Select(p => p.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string suppliedKey in values.Keys)
        {
            if (!knownKeys.Contains(suppliedKey))
            {
                error = $"unknown parameter '{suppliedKey}' for target '{connector.Key}'.";
                return false;
            }
        }

        foreach (ConnectionParameter p in connector.Parameters)
        {
            if (p.Required && string.IsNullOrWhiteSpace(values.GetValueOrDefault(p.Key)))
            {
                error = $"missing required parameter '{p.Key}' ({p.Label}) for target '{connector.Key}'.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private static string Mask(string value)
        => string.IsNullOrEmpty(value) ? value : "***";

    // Resolution order: explicit --variables path, then a variables.toml next to the
    // executable, then the built-in embedded defaults.
    private static IReadOnlyList<VariableDefinition> ResolveVariables(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
            return VariableDefinitions.Load(explicitPath);

        string bundled = Path.Combine(AppContext.BaseDirectory, VariableDefinitions.DefaultFileName);
        if (File.Exists(bundled))
            return VariableDefinitions.Load(bundled);

        return VariableDefinitions.LoadDefaults();
    }

    private static void PrintUsage()
    {
        Console.WriteLine("rtdsim - Real-Time Data Simulator CLI");
        Console.WriteLine();
        Console.WriteLine("Generates and sends load to a target service.");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  rtdsim --template <file> [--target <kind>] [--param key=value ...] [options]");
        Console.WriteLine();
        Console.WriteLine("Required:");
        Console.WriteLine("  -t, --template <file>           Path to the message template file.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("      --variables <file>          Variable definitions TOML (default: variables.toml next to the exe).");
        Console.WriteLine("      --target <kind>             Target service (default 'eventhubs').");
        Console.WriteLine("  -p, --param key=value           Connector parameter (repeatable). See per-target list below.");
        Console.WriteLine("      --parallelism <n>           Number of concurrent senders (default 1).");
        Console.WriteLine("      --batches <n>               Batches per sender (default 1).");
        Console.WriteLine("      --messages <n>              Messages per batch (default 1).");
        Console.WriteLine("      --wait <seconds>            Seconds to wait between batches (default 0).");
        Console.WriteLine("      --verify-ingestion          After the run, report Kusto ingestion failures (queued targets).");
        Console.WriteLine("  -h, --help                      Show this help.");
        Console.WriteLine();
        Console.WriteLine("Targets and their parameters:");
        foreach (ITargetConnector connector in Connectors)
        {
            PrintConnectorParameters(connector);
        }
    }

    private static void PrintConnectorParameters(ITargetConnector connector)
    {
        Console.WriteLine($"  --target {connector.Key,-12} ({connector.DisplayName})");
        foreach (ConnectionParameter p in connector.Parameters)
        {
            string flags = p.Required ? "required" : "optional";
            Console.WriteLine($"      -p {p.Key}=<value>".PadRight(34) + $"{p.Label} [{flags}]");
            if (!string.IsNullOrEmpty(p.HelpText))
            {
                Console.WriteLine($"          {p.HelpText}");
            }
        }
    }
}
