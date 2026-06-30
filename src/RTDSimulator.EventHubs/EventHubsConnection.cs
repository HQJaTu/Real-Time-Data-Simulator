using System.Text;
using Azure.Core;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using RTDSimulator.Core;

namespace RTDSimulator.EventHubs;

/// <summary>
/// <see cref="ITargetConnection"/> implementation that sends generated load to an
/// Azure Event Hubs endpoint via the Event Hubs producer client.
/// </summary>
public sealed class EventHubsConnection : ITargetConnection
{
    private readonly string _connectionString;
    private readonly string _eventHubName;
    private readonly TokenCredential? _credential;

    private EventHubProducerClient? _producerClient;

    /// <param name="connectionString">
    /// Event Hubs connection string, or the fully-qualified namespace
    /// (e.g. <c>my-namespace.servicebus.windows.net</c>) when a credential is supplied.
    /// </param>
    /// <param name="eventHubName">Name of the target event hub.</param>
    /// <param name="credential">Optional token credential for Entra ID authentication.</param>
    public EventHubsConnection(string connectionString, string eventHubName, TokenCredential? credential = null)
    {
        _connectionString = connectionString;
        _eventHubName = eventHubName;
        _credential = credential;
    }

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _producerClient ??= _credential is null
            ? new EventHubProducerClient(_connectionString, _eventHubName)
            : new EventHubProducerClient(AzureConnection.ResolveNamespace(_connectionString), _eventHubName, _credential);

        return Task.CompletedTask;
    }

    public async Task SendBatchAsync(IReadOnlyCollection<string> payloads, CancellationToken cancellationToken = default)
    {
        await ConnectAsync(cancellationToken);

        using EventDataBatch eventBatch = await _producerClient!.CreateBatchAsync(cancellationToken);
        foreach (string payload in payloads)
        {
            eventBatch.TryAdd(new EventData(Encoding.UTF8.GetBytes(payload)));
        }

        await _producerClient.SendAsync(eventBatch, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_producerClient is not null)
        {
            await _producerClient.DisposeAsync();
            _producerClient = null;
        }
    }
}
