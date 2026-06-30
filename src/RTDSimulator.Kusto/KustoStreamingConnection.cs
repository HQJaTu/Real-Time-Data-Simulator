using Azure.Core;
using Kusto.Data;
using Kusto.Ingest;

namespace RTDSimulator.Kusto;

/// <summary>
/// Ingests into a Kusto table using <b>streaming ingestion</b> (low latency). Uses the
/// engine/query endpoint and requires the streaming ingestion policy to be enabled on
/// the cluster and target table.
/// </summary>
public sealed class KustoStreamingConnection : KustoConnectionBase
{
    public KustoStreamingConnection(string clusterUri, string database, string table, string? ingestionMappingName = null, TokenCredential? credential = null)
        : base(clusterUri, database, table, ingestionMappingName, credential)
    {
    }

    protected override string ResolveEndpoint(string clusterUri) => KustoUris.ToEngine(clusterUri);

    protected override IKustoIngestClient CreateIngestClient(KustoConnectionStringBuilder connectionStringBuilder)
        => KustoIngestFactory.CreateStreamingIngestClient(connectionStringBuilder);
}
