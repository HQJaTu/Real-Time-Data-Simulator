using RTDSimulator.Core;
using Xunit;

namespace RTDSimulator.Core.Tests;

public class AzureConnectionTests
{
    [Fact]
    public void ResolveNamespace_ExtractsHostFromConnectionString()
    {
        const string cs = "Endpoint=sb://my-ns.servicebus.windows.net/;SharedAccessKeyName=key;SharedAccessKey=secret";
        Assert.Equal("my-ns.servicebus.windows.net", AzureConnection.ResolveNamespace(cs));
    }

    [Fact]
    public void ResolveNamespace_BareNamespace_IsReturnedUnchanged()
    {
        const string ns = "my-ns.servicebus.windows.net";
        Assert.Equal(ns, AzureConnection.ResolveNamespace(ns));
    }

    [Fact]
    public void ResolveNamespace_SchemeWithoutTrailingSlash_ExtractsHost()
    {
        Assert.Equal("my-ns.servicebus.windows.net", AzureConnection.ResolveNamespace("sb://my-ns.servicebus.windows.net"));
    }

    [Fact]
    public void ResolveNamespace_IsCaseInsensitiveForScheme_AndPreservesHostCase()
    {
        const string cs = "Endpoint=SB://My-NS.servicebus.windows.net/;SharedAccessKey=secret";
        Assert.Equal("My-NS.servicebus.windows.net", AzureConnection.ResolveNamespace(cs));
    }

    [Fact]
    public void ResolveNamespace_TrimsSurroundingWhitespace()
    {
        Assert.Equal("my-ns.servicebus.windows.net", AzureConnection.ResolveNamespace("  my-ns.servicebus.windows.net  "));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveNamespace_EmptyOrWhitespace_IsReturnedAsIs(string input)
    {
        Assert.Equal(input, AzureConnection.ResolveNamespace(input));
    }
}
