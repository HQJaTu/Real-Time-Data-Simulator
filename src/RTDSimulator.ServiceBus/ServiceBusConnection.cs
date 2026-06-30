using Azure.Core;
using Azure.Messaging.ServiceBus;
using RTDSimulator.Core;

namespace RTDSimulator.ServiceBus;

/// <summary>
/// <see cref="ITargetConnection"/> implementation that sends generated load to an
/// Azure Service Bus queue or topic via the Service Bus sender client.
/// </summary>
public sealed class ServiceBusConnection : ITargetConnection
{
    private readonly string _connectionString;
    private readonly string _entityName;
    private readonly TokenCredential? _credential;

    private ServiceBusClient? _client;
    private ServiceBusSender? _sender;

    /// <param name="connectionString">
    /// Service Bus connection string, or the fully-qualified namespace
    /// (e.g. <c>my-namespace.servicebus.windows.net</c>) when a credential is supplied.
    /// </param>
    /// <param name="entityName">Name of the target queue or topic.</param>
    /// <param name="credential">Optional token credential for Entra ID authentication.</param>
    public ServiceBusConnection(string connectionString, string entityName, TokenCredential? credential = null)
    {
        _connectionString = connectionString;
        _entityName = entityName;
        _credential = credential;
    }

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_client is null)
        {
            _client = _credential is null
                ? new ServiceBusClient(_connectionString)
                : new ServiceBusClient(AzureConnection.ResolveNamespace(_connectionString), _credential);
            _sender = _client.CreateSender(_entityName);
        }

        return Task.CompletedTask;
    }

    public async Task SendBatchAsync(IReadOnlyCollection<string> payloads, CancellationToken cancellationToken = default)
    {
        await ConnectAsync(cancellationToken);

        using ServiceBusMessageBatch messageBatch = await _sender!.CreateMessageBatchAsync(cancellationToken);
        foreach (string payload in payloads)
        {
            messageBatch.TryAddMessage(new ServiceBusMessage(payload));
        }

        await _sender.SendMessagesAsync(messageBatch, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_sender is not null)
        {
            await _sender.DisposeAsync();
            _sender = null;
        }

        if (_client is not null)
        {
            await _client.DisposeAsync();
            _client = null;
        }
    }
}
