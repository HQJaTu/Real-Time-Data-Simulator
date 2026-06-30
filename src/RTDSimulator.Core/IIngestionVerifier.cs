namespace RTDSimulator.Core;

/// <summary>
/// Optional capability for connections whose ingestion completes asynchronously
/// (e.g. Kusto queued ingestion), where per-batch failures cannot be observed at send
/// time. Callers can query for failures after a run to verify the data actually landed.
/// </summary>
public interface IIngestionVerifier
{
    /// <summary>
    /// Returns human-readable descriptions of ingestion failures recorded since
    /// <paramref name="sinceUtc"/>. An empty list means no failures were reported
    /// (note that asynchronous failures may lag behind the send).
    /// </summary>
    Task<IReadOnlyList<string>> GetIngestionFailuresSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken = default);
}
