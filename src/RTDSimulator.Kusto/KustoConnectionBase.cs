using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Ingestion;
using Kusto.Data.Net.Client;
using Kusto.Ingest;
using RTDSimulator.Core;

namespace RTDSimulator.Kusto;

/// <summary>
/// Shared base for the Kusto connections. Each batch of payloads is ingested into a
/// database table as newline-separated JSON objects (multijson). Subclasses choose the
/// endpoint form and the ingest-client kind (streaming vs. queued).
/// </summary>
public abstract class KustoConnectionBase : ITargetConnection, IIngestionVerifier
{
    private readonly string _clusterUri;
    private readonly string _database;
    private readonly string _table;
    private readonly string? _ingestionMappingName;
    private readonly TokenCredential _credential;

    private IKustoIngestClient? _ingestClient;
    private KustoIngestionProperties? _ingestionProperties;

    protected KustoConnectionBase(string clusterUri, string database, string table, string? ingestionMappingName, TokenCredential? credential)
    {
        _clusterUri = clusterUri;
        _database = database;
        _table = table;
        _ingestionMappingName = ingestionMappingName;
        _credential = credential ?? new DefaultAzureCredential();
    }

    /// <summary>Maps the supplied cluster URI to the endpoint this ingestion mode needs.</summary>
    protected abstract string ResolveEndpoint(string clusterUri);

    /// <summary>Creates the streaming or queued ingest client.</summary>
    protected abstract IKustoIngestClient CreateIngestClient(KustoConnectionStringBuilder connectionStringBuilder);

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_ingestClient is null)
        {
            var kcsb = new KustoConnectionStringBuilder(ResolveEndpoint(_clusterUri))
                .WithAadAzureTokenCredentialsAuthentication(_credential);

            _ingestClient = CreateIngestClient(kcsb);
            _ingestionProperties = new KustoIngestionProperties(_database, _table)
            {
                Format = DataSourceFormat.multijson,
            };

            // Without a mapping, Kusto maps JSON properties to columns by name
            // (case-sensitive); mismatched names produce all-null rows. A named
            // mapping lets arbitrary field names map to the intended columns.
            if (!string.IsNullOrWhiteSpace(_ingestionMappingName))
            {
                _ingestionProperties.IngestionMapping = new IngestionMapping
                {
                    IngestionMappingReference = _ingestionMappingName,
                    IngestionMappingKind = IngestionMappingKind.Json,
                };
            }
        }

        return Task.CompletedTask;
    }

    public async Task SendBatchAsync(IReadOnlyCollection<string> payloads, CancellationToken cancellationToken = default)
    {
        // #2: fail fast on malformed JSON before touching the service — multijson
        // ingestion requires each message to be a JSON object.
        ValidatePayloadsAreJson(payloads);

        await ConnectAsync(cancellationToken);

        var stream = new MemoryStream();
        await using (var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true))
        {
            foreach (string payload in payloads)
            {
                await writer.WriteAsync(payload);
                await writer.WriteAsync('\n');
            }
        }
        stream.Position = 0;

        IKustoIngestionResult result = await _ingestClient!.IngestFromStreamAsync(
            stream,
            _ingestionProperties!,
            new StreamSourceOptions { LeaveOpen = false });

        // #1: surface any synchronously-reported failure (streaming reports the real
        // outcome here; queued reports 'Queued' and is verified after the run instead).
        ThrowIfIngestionFailed(result);
    }

    private static void ValidatePayloadsAreJson(IReadOnlyCollection<string> payloads)
    {
        int index = 0;
        foreach (string payload in payloads)
        {
            try
            {
                using var _ = JsonDocument.Parse(payload);
            }
            catch (JsonException ex)
            {
                throw new FormatException(
                    $"Payload #{index} is not valid JSON (Kusto multijson ingestion requires JSON objects): {ex.Message}", ex);
            }
            index++;
        }
    }

    private static void ThrowIfIngestionFailed(IKustoIngestionResult result)
    {
        foreach (IngestionStatus status in result.GetIngestionStatusCollection())
        {
            if (status.Status == Status.Failed)
            {
                throw new InvalidOperationException(
                    $"Kusto ingestion failed ({status.ErrorCode}): {status.Details}");
            }
        }
    }

    public async Task<IReadOnlyList<string>> GetIngestionFailuresSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        // Ingestion failures are recorded cluster-side; query the engine endpoint.
        var kcsb = new KustoConnectionStringBuilder(KustoUris.ToEngine(_clusterUri))
            .WithAadAzureTokenCredentialsAuthentication(_credential);

        string since = sinceUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");
        string command =
            $".show ingestion failures | where Table == '{_table}' and FailedOn > datetime({since}) " +
            "| project FailedOn, FailureKind, Details";

        var failures = new List<string>();
        using ICslAdminProvider admin = KustoClientFactory.CreateCslAdminProvider(kcsb);
        using System.Data.IDataReader reader = await Task.Run(
            () => admin.ExecuteControlCommand(_database, command), cancellationToken);

        while (reader.Read())
        {
            failures.Add($"{reader["FailedOn"]:o} [{reader["FailureKind"]}] {reader["Details"]}");
        }

        return failures;
    }

    public ValueTask DisposeAsync()
    {
        _ingestClient?.Dispose();
        _ingestClient = null;
        return ValueTask.CompletedTask;
    }
}
