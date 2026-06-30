using RTDSimulator.Core;
using Xunit;

namespace RTDSimulator.Core.Tests;

public class VariableDefinitionsTests
{
    private const string Sample = """
        [UserId]
        type = "randomInt"
        min = 5000
        max = 5100

        [Env]
        type = "randomItem"
        items = ["dev", "prod"]

        ["FuelType(MessageIndex)"]
        type = "indexedItem"
        items = ["A", "B", "C"]

        ["DateTime.Now"]
        type = "dateTimeNow"

        [Fixed]
        type = "literal"
        value = "42"
        """;

    [Fact]
    public void Parse_ReadsAllDefinitions_WithNameAsTableHeader()
    {
        var defs = VariableDefinitions.Parse(Sample);
        Assert.Equal(5, defs.Count);
        Assert.Equal(
            new[] { "UserId", "Env", "FuelType(MessageIndex)", "DateTime.Now", "Fixed" },
            defs.Select(d => d.Name));
    }

    [Fact]
    public void Parse_MapsTypesAndParameters()
    {
        var byName = VariableDefinitions.Parse(Sample).ToDictionary(d => d.Name);

        Assert.Equal(VariableType.RandomInt, byName["UserId"].Type);
        Assert.Equal(5000, byName["UserId"].Min);
        Assert.Equal(5100, byName["UserId"].Max);

        Assert.Equal(VariableType.RandomItem, byName["Env"].Type);
        Assert.Equal(new[] { "dev", "prod" }, byName["Env"].Items);

        Assert.Equal(VariableType.IndexedItem, byName["FuelType(MessageIndex)"].Type);
        Assert.Equal(VariableType.DateTimeNow, byName["DateTime.Now"].Type);

        Assert.Equal(VariableType.Literal, byName["Fixed"].Type);
        Assert.Equal("42", byName["Fixed"].Value);
    }

    [Theory]
    [InlineData(0, "A")]
    [InlineData(1, "B")]
    [InlineData(3, "A")] // wraps
    public void Evaluate_IndexedItem_SelectsByMessageIndex(int index, string expected)
    {
        var def = VariableDefinitions.Parse(Sample).Single(d => d.Name == "FuelType(MessageIndex)");
        Assert.Equal(expected, def.Evaluate(index, new Random()));
    }

    [Fact]
    public void Evaluate_RandomInt_IsWithinRange()
    {
        var def = VariableDefinitions.Parse(Sample).Single(d => d.Name == "UserId");
        int value = int.Parse(def.Evaluate(0, new Random()));
        Assert.InRange(value, 5000, 5099); // max exclusive
    }

    [Fact]
    public void LoadDefaults_ReturnsTheBuiltInVariables()
    {
        var byName = VariableDefinitions.LoadDefaults().ToDictionary(d => d.Name);

        Assert.Equal(6, byName.Count);
        Assert.Equal(VariableType.RandomInt, byName["UserId"].Type);
        Assert.Equal(VariableType.RandomItem, byName["Device"].Type);
        Assert.Equal(VariableType.IndexedItem, byName["FuelType(MessageIndex)"].Type);
        Assert.Equal(VariableType.DateTimeNow, byName["DateTime.Now"].Type);
        Assert.Equal("48", byName["SettlementPeriod"].Value);
    }

    [Fact]
    public async Task PayloadGenerator_UsesSuppliedDefinitions()
    {
        var defs = VariableDefinitions.Parse("""
            [Greeting]
            type = "literal"
            value = "hi"
            """);
        var gen = new PayloadGenerator("{{Greeting}}, world", defs);
        Assert.Equal("hi, world", await gen.GetPayload(0));
    }

    [Fact]
    public void Parse_UnknownType_Throws()
    {
        Assert.Throws<FormatException>(() => VariableDefinitions.Parse("""
            [X]
            type = "bogus"
            """));
    }

    [Fact]
    public void Parse_MissingRequiredParameter_Throws()
    {
        Assert.Throws<FormatException>(() => VariableDefinitions.Parse("""
            [X]
            type = "randomInt"
            min = 1
            """)); // missing max
    }

    [Fact]
    public void Parse_NonTableEntry_Throws()
    {
        Assert.Throws<FormatException>(() => VariableDefinitions.Parse("Loose = 123"));
    }
}
