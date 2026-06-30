using Azure.Core;
using Kusto.Data;
using Kusto.Ingest;

namespace RTDSimulator.Kusto;

/// <summary>
/// Ingests into a Kusto table using <b>queued ingestion</b> (batched server-side). Uses
/// the data-management (<c>ingest-</c>) endpoint and needs no streaming policy, so it
/// works against any cluster/table out of the box.
/// </summary>
public sealed class KustoQueuedConnection : KustoConnectionBase
{
    public KustoQueuedConnection(string clusterUri, string database, string table, string? ingestionMappingName = null, TokenCredential? credential = null)
        : base(clusterUri, database, table, ingestionMappingName, credential)
    {
    }

    protected override string ResolveEndpoint(string clusterUri) => KustoUris.ToIngest(clusterUri);

    protected override IKustoIngestClient CreateIngestClient(KustoConnectionStringBuilder connectionStringBuilder)
        => KustoIngestFactory.CreateQueuedIngestClient(connectionStringBuilder);
}
