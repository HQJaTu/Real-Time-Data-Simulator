namespace RTDSimulator.Core;

/// <summary>
/// Abstraction over a target service that generated load can be sent to.
/// Implement this interface to add support for additional service types
/// (Azure Service Bus / Event Hubs, Kafka, HTTP endpoints, ...).
/// </summary>
public interface ITargetConnection : IAsyncDisposable
{
    /// <summary>
    /// Establishes (or validates) the connection to the target service.
    /// Implementations should be safe to call more than once.
    /// </summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a single batch of payloads to the target service.
    /// </summary>
    Task SendBatchAsync(IReadOnlyCollection<string> payloads, CancellationToken cancellationToken = default);
}
