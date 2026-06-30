using System.Globalization;
using RTDSimulator.Core;
using Xunit;

namespace RTDSimulator.Core.Tests;

public class PayloadGeneratorTests
{
    // --- Plain text / passthrough ---

    [Fact]
    public async Task PlainText_IsReturnedUnchanged()
    {
        var gen = new PayloadGenerator("hello world");
        Assert.Equal("hello world", await gen.GetPayload(0));
    }

    [Fact]
    public async Task UnknownPlaceholder_IsLeftUntouched()
    {
        var gen = new PayloadGenerator("{{DoesNotExist}}");
        Assert.Equal("{{DoesNotExist}}", await gen.GetPayload(0));
    }

    // --- Built-in variables ---

    [Fact]
    public async Task SettlementPeriod_ReplacedWithLiteral()
    {
        var gen = new PayloadGenerator("p={{SettlementPeriod}}");
        Assert.Equal("p=48", await gen.GetPayload(0));
    }

    [Fact]
    public async Task MultipleOccurrencesOfVariable_AreAllReplaced()
    {
        var gen = new PayloadGenerator("{{SettlementPeriod}}-{{SettlementPeriod}}");
        Assert.Equal("48-48", await gen.GetPayload(0));
    }

    [Fact]
    public async Task UserId_IsWithinConfiguredRange()
    {
        var gen = new PayloadGenerator("{{UserId}}");
        int value = int.Parse(await gen.GetPayload(0));
        Assert.InRange(value, 5000, 5099); // Random(5000,5100) -> max exclusive
    }

    [Fact]
    public async Task ProductId_IsWithinConfiguredRange()
    {
        var gen = new PayloadGenerator("{{ProductId}}");
        int value = int.Parse(await gen.GetPayload(0));
        Assert.InRange(value, 700, 998); // Random(700,999) -> max exclusive
    }

    [Fact]
    public async Task Device_IsOneOfAllowedItems()
    {
        var gen = new PayloadGenerator("{{Device}}");
        string value = await gen.GetPayload(0);
        Assert.Contains(value, new[] { "mobile", "tablet", "pc" });
    }

    [Theory]
    [InlineData(0, "BIOMASS")]
    [InlineData(1, "CCGT")]
    [InlineData(2, "COAL")]
    [InlineData(19, "BIOMASS")] // wraps: 19 % 19 == 0
    public async Task FuelType_IteratesByMessageIndex(int index, string expected)
    {
        var gen = new PayloadGenerator("{{FuelType(MessageIndex)}}");
        Assert.Equal(expected, await gen.GetPayload(index));
    }

    [Fact]
    public async Task DateTimeNowVariable_ProducesParseableTimestamp()
    {
        var gen = new PayloadGenerator("{{DateTime.Now}}");
        string value = await gen.GetPayload(0);
        Assert.True(DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _));
    }

    // --- Expressions ({{$ ... }}) ---

    [Fact]
    public async Task Expression_ArithmeticIsEvaluated()
    {
        var gen = new PayloadGenerator("{{$1+2}}");
        Assert.Equal("3", await gen.GetPayload(0));
    }

    [Fact]
    public async Task Expression_HasSystemNamespaceImported()
    {
        var gen = new PayloadGenerator("{{$Math.Max(3, 7)}}");
        Assert.Equal("7", await gen.GetPayload(0));
    }

    [Fact]
    public async Task DistinctExpressions_AreEvaluatedIndependently()
    {
        var gen = new PayloadGenerator("{{$1+1}}/{{$2+2}}");
        Assert.Equal("2/4", await gen.GetPayload(0));
    }

    // --- RandomString helper ---

    [Fact]
    public async Task RandomString_HasRequestedLength()
    {
        var gen = new PayloadGenerator("{{$RandomString(12)}}");
        Assert.Equal(12, (await gen.GetPayload(0)).Length);
    }

    [Fact]
    public async Task RandomString_DefaultCharset_IsAlphanumeric()
    {
        var gen = new PayloadGenerator("{{$RandomString(200)}}");
        Assert.Matches("^[A-Za-z0-9]+$", await gen.GetPayload(0));
    }

    [Fact]
    public async Task RandomString_RespectsCustomCharset()
    {
        var gen = new PayloadGenerator("{{$RandomString(50, \"AB\")}}");
        string value = await gen.GetPayload(0);
        Assert.Equal(50, value.Length);
        Assert.Matches("^[AB]+$", value);
    }

    [Fact]
    public async Task RandomString_ZeroLength_IsEmpty()
    {
        var gen = new PayloadGenerator("x{{$RandomString(0)}}y");
        Assert.Equal("xy", await gen.GetPayload(0));
    }

    [Fact]
    public async Task IdenticalExpressionTokens_ProduceTheSameValueWithinAMessage()
    {
        // The engine evaluates each distinct token once and replaces all occurrences,
        // so two identical tokens resolve to the same value.
        var gen = new PayloadGenerator("{{$RandomString(16)}}|{{$RandomString(16)}}");
        string[] parts = (await gen.GetPayload(0)).Split('|');
        Assert.Equal(parts[0], parts[1]);
    }

    // --- Combined / realistic template ---

    [Fact]
    public async Task RealisticTemplate_AllPlaceholdersResolved()
    {
        const string template = """
            { "id": "{{$RandomString(8)}}", "device": "{{Device}}", "period": {{SettlementPeriod}} }
            """;
        var gen = new PayloadGenerator(template);
        string result = await gen.GetPayload(0);

        Assert.DoesNotContain("{{", result);
        Assert.DoesNotContain("}}", result);
        Assert.Contains("\"period\": 48", result);
    }
}
