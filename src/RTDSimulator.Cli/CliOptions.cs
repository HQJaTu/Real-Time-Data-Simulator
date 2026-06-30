namespace RTDSimulator.Cli;

/// <summary>
/// Parsed command-line options for the simulator CLI. Connection parameters are kept
/// generic (a key/value bag) so the set of options follows whichever connector the
/// <c>--target</c> selects, rather than being hard-coded here.
/// </summary>
internal sealed class CliOptions
{
    public required string TargetKey { get; init; }
    public required Dictionary<string, string> Parameters { get; init; }
    public required string TemplatePath { get; init; }
    public string? VariablesPath { get; init; }
    public bool VerifyIngestion { get; init; }
    public int Parallelism { get; init; } = 1;
    public int BatchesPerSender { get; init; } = 1;
    public int MessagesPerBatch { get; init; } = 1;
    public int WaitTimeSeconds { get; init; }

    /// <summary>
    /// Parses the supplied arguments. Returns <c>null</c> when parsing fails or
    /// help was requested; in the failure case <paramref name="error"/> is set.
    /// </summary>
    public static CliOptions? Parse(string[] args, out string? error)
    {
        error = null;

        string targetKey = "eventhubs";
        string? templatePath = null;
        string? variablesPath = null;
        bool verifyIngestion = false;
        var parameters = new Dictionary<string, string>();
        int parallelism = 1;
        int batches = 1;
        int messages = 1;
        int wait = 0;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            string name = arg;
            string? inlineValue = null;

            int eq = arg.IndexOf('=');
            if (arg.StartsWith("--") && eq > 0)
            {
                name = arg.Substring(0, eq);
                inlineValue = arg.Substring(eq + 1);
            }

            switch (name)
            {
                case "-h":
                case "--help":
                    return null;

                case "--target":
                    targetKey = NextValue(args, ref i, inlineValue, name, ref error) ?? targetKey;
                    break;
                case "-t":
                case "--template":
                    templatePath = NextValue(args, ref i, inlineValue, name, ref error);
                    break;
                case "--variables":
                    variablesPath = NextValue(args, ref i, inlineValue, name, ref error);
                    break;
                case "--verify-ingestion":
                    verifyIngestion = true;
                    break;
                case "-p":
                case "--param":
                    AddParam(NextValue(args, ref i, inlineValue, name, ref error), parameters, ref error);
                    break;
                case "--parallelism":
                    parallelism = ParseInt(NextValue(args, ref i, inlineValue, name, ref error), name, ref error, parallelism);
                    break;
                case "--batches":
                    batches = ParseInt(NextValue(args, ref i, inlineValue, name, ref error), name, ref error, batches);
                    break;
                case "--messages":
                    messages = ParseInt(NextValue(args, ref i, inlineValue, name, ref error), name, ref error, messages);
                    break;
                case "--wait":
                    wait = ParseInt(NextValue(args, ref i, inlineValue, name, ref error), name, ref error, wait);
                    break;
                default:
                    error = $"unknown argument '{arg}'.";
                    return null;
            }

            if (error is not null)
                return null;
        }

        if (string.IsNullOrWhiteSpace(templatePath))
        {
            error = "missing required --template.";
            return null;
        }

        return new CliOptions
        {
            TargetKey = targetKey,
            Parameters = parameters,
            TemplatePath = templatePath,
            VariablesPath = variablesPath,
            VerifyIngestion = verifyIngestion,
            Parallelism = parallelism,
            BatchesPerSender = batches,
            MessagesPerBatch = messages,
            WaitTimeSeconds = wait,
        };
    }

    private static void AddParam(string? value, Dictionary<string, string> parameters, ref string? error)
    {
        if (error is not null)
            return;
        if (value is null)
            return;

        int eq = value.IndexOf('=');
        if (eq <= 0)
        {
            error = $"--param expects 'key=value', got '{value}'.";
            return;
        }

        string key = value.Substring(0, eq);
        string val = value.Substring(eq + 1);
        parameters[key] = val;
    }

    private static string? NextValue(string[] args, ref int i, string? inlineValue, string name, ref string? error)
    {
        if (inlineValue is not null)
            return inlineValue;

        if (i + 1 >= args.Length)
        {
            error = $"missing value for '{name}'.";
            return null;
        }

        return args[++i];
    }

    private static int ParseInt(string? value, string name, ref string? error, int fallback)
    {
        if (error is not null)
            return fallback;
        if (int.TryParse(value, out int parsed))
            return parsed;

        error = $"'{name}' expects an integer value.";
        return fallback;
    }
}
