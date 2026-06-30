namespace RTDSimulator.Kusto;

/// <summary>
/// Helpers for switching a Kusto cluster URI between its engine/query endpoint
/// (used for streaming ingestion) and its data-management endpoint, which carries an
/// <c>ingest-</c> host prefix (used for queued ingestion). Both connectors therefore
/// accept the same "Cluster URI" and derive the endpoint they need.
/// </summary>
internal static class KustoUris
{
    private const string IngestPrefix = "ingest-";

    /// <summary>Returns the data-management (<c>ingest-</c>) endpoint for queued ingestion.</summary>
    public static string ToIngest(string clusterUri) => WithIngestPrefix(clusterUri, add: true);

    /// <summary>Returns the engine/query endpoint (no <c>ingest-</c> prefix) for streaming ingestion.</summary>
    public static string ToEngine(string clusterUri) => WithIngestPrefix(clusterUri, add: false);

    private static string WithIngestPrefix(string clusterUri, bool add)
    {
        if (!Uri.TryCreate(clusterUri, UriKind.Absolute, out Uri? uri))
            return clusterUri;

        bool hasPrefix = uri.Host.StartsWith(IngestPrefix, StringComparison.OrdinalIgnoreCase);
        string host;
        if (add && !hasPrefix)
            host = IngestPrefix + uri.Host;
        else if (!add && hasPrefix)
            host = uri.Host.Substring(IngestPrefix.Length);
        else
            return clusterUri; // already in the desired form

        string portPart = uri.IsDefaultPort ? string.Empty : ":" + uri.Port;
        string path = uri.AbsolutePath == "/" ? string.Empty : uri.AbsolutePath;
        return $"{uri.Scheme}://{host}{portPart}{path}";
    }
}
