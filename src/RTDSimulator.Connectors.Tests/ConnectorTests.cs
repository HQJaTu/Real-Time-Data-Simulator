using RTDSimulator.Core;
using RTDSimulator.EventHubs;
using RTDSimulator.Kusto;
using RTDSimulator.ServiceBus;
using Xunit;

namespace RTDSimulator.Connectors.Tests;

public class ConnectorTests
{
    // --- Shared descriptor invariants ---

    private static void AssertValidDescriptor(ITargetConnector connector)
    {
        Assert.False(string.IsNullOrWhiteSpace(connector.Key));
        Assert.False(string.IsNullOrWhiteSpace(connector.DisplayName));
        Assert.NotEmpty(connector.Parameters);

        var keys = connector.Parameters.Select(p => p.Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(connector.Parameters, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Key));
            Assert.False(string.IsNullOrWhiteSpace(p.Label));
        });
    }

    [Fact]
    public void EventHubs_HasValidDescriptor() => AssertValidDescriptor(new EventHubsConnector());

    [Fact]
    public void ServiceBus_HasValidDescriptor() => AssertValidDescriptor(new ServiceBusConnector());

    [Fact]
    public void KustoStreaming_HasValidDescriptor() => AssertValidDescriptor(new KustoStreamingConnector());

    [Fact]
    public void KustoQueued_HasValidDescriptor() => AssertValidDescriptor(new KustoQueuedConnector());

    // --- Event Hubs ---

    [Fact]
    public void EventHubs_DeclaresExpectedParameters()
    {
        var connector = new EventHubsConnector();

        Assert.Equal("eventhubs", connector.Key);
        Assert.Equal("Event Hubs", connector.DisplayName);
        Assert.Equal(
            new[] { EventHubsConnector.ConnectionKey, EventHubsConnector.EventHubKey },
            connector.Parameters.Select(p => p.Key));

        ConnectionParameter connection = connector.Parameters.Single(p => p.Key == EventHubsConnector.ConnectionKey);
        Assert.True(connection.Secret);
        Assert.True(connection.Required);
    }

    [Fact]
    public void EventHubs_CreateConnection_ReturnsEventHubsConnection()
    {
        var values = new Dictionary<string, string>
        {
            [EventHubsConnector.ConnectionKey] = "Endpoint=sb://ns.servicebus.windows.net/;SharedAccessKey=k",
            [EventHubsConnector.EventHubKey] = "hub",
        };

        ITargetConnection connection = new EventHubsConnector().CreateConnection(values);
        Assert.IsType<EventHubsConnection>(connection);
    }

    // --- Service Bus ---

    [Fact]
    public void ServiceBus_UsesQueueOrTopicTerminology()
    {
        var connector = new ServiceBusConnector();

        Assert.Equal("servicebus", connector.Key);
        Assert.Equal("Azure Service Bus", connector.DisplayName);
        Assert.Equal(
            new[] { ServiceBusConnector.ConnectionKey, ServiceBusConnector.EntityKey },
            connector.Parameters.Select(p => p.Key));

        ConnectionParameter entity = connector.Parameters.Single(p => p.Key == ServiceBusConnector.EntityKey);
        Assert.Equal("Queue / Topic name", entity.Label);
    }

    [Fact]
    public void ServiceBus_CreateConnection_ReturnsServiceBusConnection()
    {
        var values = new Dictionary<string, string>
        {
            [ServiceBusConnector.ConnectionKey] = "Endpoint=sb://ns.servicebus.windows.net/;SharedAccessKey=k",
            [ServiceBusConnector.EntityKey] = "my-queue",
        };

        ITargetConnection connection = new ServiceBusConnector().CreateConnection(values);
        Assert.IsType<ServiceBusConnection>(connection);
    }

    // --- Kusto ---

    [Theory]
    [InlineData("kusto-streaming")]
    [InlineData("kusto-queued")]
    public void Kusto_DeclaresClusterDatabaseAndTable_AllRequired(string key)
    {
        ITargetConnector connector = key == "kusto-streaming"
            ? new KustoStreamingConnector()
            : new KustoQueuedConnector();

        Assert.Equal(key, connector.Key);
        Assert.Equal(
            new[] { KustoParameters.ClusterUriKey, KustoParameters.DatabaseKey, KustoParameters.TableKey, KustoParameters.MappingKey },
            connector.Parameters.Select(p => p.Key));

        // Cluster/database/table are required; the ingestion mapping is optional.
        Assert.True(connector.Parameters.Single(p => p.Key == KustoParameters.ClusterUriKey).Required);
        Assert.True(connector.Parameters.Single(p => p.Key == KustoParameters.DatabaseKey).Required);
        Assert.True(connector.Parameters.Single(p => p.Key == KustoParameters.TableKey).Required);
        Assert.False(connector.Parameters.Single(p => p.Key == KustoParameters.MappingKey).Required);
    }

    [Fact]
    public void KustoStreaming_CreateConnection_ReturnsStreamingConnection()
    {
        ITargetConnection connection = new KustoStreamingConnector().CreateConnection(KustoValues());
        Assert.IsType<KustoStreamingConnection>(connection);
    }

    [Fact]
    public void KustoQueued_CreateConnection_ReturnsQueuedConnection()
    {
        ITargetConnection connection = new KustoQueuedConnector().CreateConnection(KustoValues());
        Assert.IsType<KustoQueuedConnection>(connection);
    }

    private static Dictionary<string, string> KustoValues() => new()
    {
        [KustoParameters.ClusterUriKey] = "https://mycluster.westeurope.kusto.windows.net",
        [KustoParameters.DatabaseKey] = "db",
        [KustoParameters.TableKey] = "MyTable",
    };

    [Fact]
    public async Task Kusto_SendBatch_RejectsMalformedJson_BeforeContactingService()
    {
        // Validation happens before any network call, so this fails fast without a live cluster.
        var connection = new KustoStreamingConnection("https://mycluster.westeurope.kusto.windows.net", "db", "t");
        await Assert.ThrowsAsync<FormatException>(
            () => connection.SendBatchAsync(new[] { "{ not valid json" }));
    }

    [Fact]
    public void Kusto_ConnectionsImplement_IIngestionVerifier()
    {
        Assert.IsAssignableFrom<IIngestionVerifier>(
            new KustoQueuedConnector().CreateConnection(KustoValues()));
        Assert.IsAssignableFrom<IIngestionVerifier>(
            new KustoStreamingConnector().CreateConnection(KustoValues()));
    }

    // --- Missing values do not throw at construction (validation lives in the UI/CLI) ---

    [Fact]
    public void CreateConnection_WithMissingValues_DoesNotThrow()
    {
        var empty = new Dictionary<string, string>();

        Assert.NotNull(new EventHubsConnector().CreateConnection(empty));
        Assert.NotNull(new ServiceBusConnector().CreateConnection(empty));
        Assert.NotNull(new KustoStreamingConnector().CreateConnection(empty));
        Assert.NotNull(new KustoQueuedConnector().CreateConnection(empty));
    }
}
