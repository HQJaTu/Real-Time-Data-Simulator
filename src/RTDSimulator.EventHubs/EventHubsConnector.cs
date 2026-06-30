using Azure.Core;
using RTDSimulator.Core;

namespace RTDSimulator.EventHubs;

/// <summary>
/// <see cref="ITargetConnector"/> describing an Azure Event Hubs target.
/// </summary>
public sealed class EventHubsConnector : ITargetConnector
{
    public const string ConnectionKey = "connection";
    public const string EventHubKey = "eventHub";

    public string Key => "eventhubs";

    public string DisplayName => "Event Hubs";

    public IReadOnlyList<ConnectionParameter> Parameters { get; } = new[]
    {
        new ConnectionParameter(
            ConnectionKey,
            "Connection string / Namespace",
            secret: true,
            defaultValue: "Endpoint=sb://****fkawjq507mc.servicebus.windows.net/;SharedAccessKeyName=key_0000;SharedAccessKey=****",
            helpText: "SAS connection string, or the fully-qualified namespace when signed in with Azure Identity."),
        new ConnectionParameter(
            EventHubKey,
            "Event Hub name",
            defaultValue: "es_08960000000000000000"),
    };

    public ITargetConnection CreateConnection(IReadOnlyDictionary<string, string> values, TokenCredential? credential = null)
    {
        string connection = values.GetValueOrDefault(ConnectionKey, string.Empty);
        string eventHub = values.GetValueOrDefault(EventHubKey, string.Empty);
        return new EventHubsConnection(connection, eventHub, credential);
    }
}
