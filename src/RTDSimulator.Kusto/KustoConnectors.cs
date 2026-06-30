using Azure.Core;
using RTDSimulator.Core;

namespace RTDSimulator.Kusto;

/// <summary>Shared parameter keys for the Kusto connectors.</summary>
public static class KustoParameters
{
    public const string ClusterUriKey = "clusterUri";
    public const string DatabaseKey = "database";
    public const string TableKey = "table";
    public const string MappingKey = "mapping";

    internal const string DefaultClusterUri = "https://mycluster.westeurope.kusto.windows.net";

    internal const string MappingHelp =
        "Optional. Name of a JSON ingestion mapping on the table. Without a mapping, " +
        "JSON property names must exactly match column names (case-sensitive) or rows ingest as nulls.";
}

/// <summary>
/// <see cref="ITargetConnector"/> for Azure Data Explorer (Kusto) using streaming
/// ingestion. Authentication uses Azure Identity (GUI sign-in or the CLI credential chain).
/// </summary>
public sealed class KustoStreamingConnector : ITargetConnector
{
    public string Key => "kusto-streaming";

    public string DisplayName => "Azure Data Explorer — Streaming ingestion";

    public IReadOnlyList<ConnectionParameter> Parameters { get; } = new[]
    {
        new ConnectionParameter(
            KustoParameters.ClusterUriKey,
            "Cluster URI",
            defaultValue: KustoParameters.DefaultClusterUri,
            helpText: "Engine/query cluster URI. Streaming ingestion must be enabled on the cluster and target table."),
        new ConnectionParameter(KustoParameters.DatabaseKey, "Database"),
        new ConnectionParameter(KustoParameters.TableKey, "Table"),
        new ConnectionParameter(KustoParameters.MappingKey, "Ingestion mapping", required: false, helpText: KustoParameters.MappingHelp),
    };

    public ITargetConnection CreateConnection(IReadOnlyDictionary<string, string> values, TokenCredential? credential = null)
        => new KustoStreamingConnection(
            values.GetValueOrDefault(KustoParameters.ClusterUriKey, string.Empty),
            values.GetValueOrDefault(KustoParameters.DatabaseKey, string.Empty),
            values.GetValueOrDefault(KustoParameters.TableKey, string.Empty),
            values.GetValueOrDefault(KustoParameters.MappingKey, string.Empty),
            credential);
}

/// <summary>
/// <see cref="ITargetConnector"/> for Azure Data Explorer (Kusto) using queued ingestion.
/// Requires no streaming policy, so it works against any cluster/table without setup.
/// </summary>
public sealed class KustoQueuedConnector : ITargetConnector
{
    public string Key => "kusto-queued";

    public string DisplayName => "Azure Data Explorer — Queued ingestion";

    public IReadOnlyList<ConnectionParameter> Parameters { get; } = new[]
    {
        new ConnectionParameter(
            KustoParameters.ClusterUriKey,
            "Cluster URI",
            defaultValue: KustoParameters.DefaultClusterUri,
            helpText: "Engine/query cluster URI (the 'ingest-' data-management endpoint is derived automatically). No streaming policy required."),
        new ConnectionParameter(KustoParameters.DatabaseKey, "Database"),
        new ConnectionParameter(KustoParameters.TableKey, "Table"),
        new ConnectionParameter(KustoParameters.MappingKey, "Ingestion mapping", required: false, helpText: KustoParameters.MappingHelp),
    };

    public ITargetConnection CreateConnection(IReadOnlyDictionary<string, string> values, TokenCredential? credential = null)
        => new KustoQueuedConnection(
            values.GetValueOrDefault(KustoParameters.ClusterUriKey, string.Empty),
            values.GetValueOrDefault(KustoParameters.DatabaseKey, string.Empty),
            values.GetValueOrDefault(KustoParameters.TableKey, string.Empty),
            values.GetValueOrDefault(KustoParameters.MappingKey, string.Empty),
            credential);
}
