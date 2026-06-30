using RTDSimulator.Kusto;
using Xunit;

namespace RTDSimulator.Connectors.Tests;

public class KustoUrisTests
{
    [Fact]
    public void ToIngest_AddsIngestPrefix()
        => Assert.Equal("https://ingest-c.westeurope.kusto.windows.net",
            KustoUris.ToIngest("https://c.westeurope.kusto.windows.net"));

    [Fact]
    public void ToIngest_LeavesAlreadyIngestUnchanged()
        => Assert.Equal("https://ingest-c.westeurope.kusto.windows.net",
            KustoUris.ToIngest("https://ingest-c.westeurope.kusto.windows.net"));

    [Fact]
    public void ToEngine_StripsIngestPrefix()
        => Assert.Equal("https://c.westeurope.kusto.windows.net",
            KustoUris.ToEngine("https://ingest-c.westeurope.kusto.windows.net"));

    [Fact]
    public void ToEngine_LeavesEngineUnchanged()
        => Assert.Equal("https://c.westeurope.kusto.windows.net",
            KustoUris.ToEngine("https://c.westeurope.kusto.windows.net"));

    [Fact]
    public void ToIngest_DropsTrailingSlash()
        => Assert.Equal("https://ingest-c.kusto.windows.net",
            KustoUris.ToIngest("https://c.kusto.windows.net/"));

    [Fact]
    public void RoundTrip_EngineToIngestToEngine()
    {
        const string engine = "https://mycluster.westeurope.kusto.windows.net";
        Assert.Equal(engine, KustoUris.ToEngine(KustoUris.ToIngest(engine)));
    }

    [Fact]
    public void NonUri_ReturnedUnchanged()
        => Assert.Equal("not a uri", KustoUris.ToIngest("not a uri"));
}
