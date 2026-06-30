using Azure.Core;
using RTDSimulator.Core;

namespace RTDSimulator.ServiceBus;

/// <summary>
/// <see cref="ITargetConnector"/> describing an Azure Service Bus target (queue or topic).
/// </summary>
public sealed class ServiceBusConnector : ITargetConnector
{
    public const string ConnectionKey = "connection";
    public const string EntityKey = "entity";

    public string Key => "servicebus";

    public string DisplayName => "Azure Service Bus";

    public IReadOnlyList<ConnectionParameter> Parameters { get; } = new[]
    {
        new ConnectionParameter(
            ConnectionKey,
            "Connection string / Namespace",
            secret: true,
            helpText: "SAS connection string, or the fully-qualified namespace when signed in with Azure Identity."),
        new ConnectionParameter(
            EntityKey,
            "Queue / Topic name"),
    };

    public ITargetConnection CreateConnection(IReadOnlyDictionary<string, string> values, TokenCredential? credential = null)
    {
        string connection = values.GetValueOrDefault(ConnectionKey, string.Empty);
        string entity = values.GetValueOrDefault(EntityKey, string.Empty);
        return new ServiceBusConnection(connection, entity, credential);
    }
}
