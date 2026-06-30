namespace RTDSimulator.Core;

/// <summary>The kind of value a <see cref="VariableDefinition"/> produces.</summary>
public enum VariableType
{
    /// <summary>A fixed string (<see cref="VariableDefinition.Value"/>).</summary>
    Literal,

    /// <summary>Random integer in <c>[Min, Max)</c> (max exclusive).</summary>
    RandomInt,

    /// <summary>A random element of <see cref="VariableDefinition.Items"/>.</summary>
    RandomItem,

    /// <summary>An element of <see cref="VariableDefinition.Items"/> chosen by message index (<c>index % count</c>).</summary>
    IndexedItem,

    /// <summary>Current local date/time, formatted with <see cref="VariableDefinition.Format"/> (default <c>"O"</c>).</summary>
    DateTimeNow,
}

/// <summary>
/// A soft-coded variable definition, typically loaded from a TOML file. The
/// <see cref="Name"/> is the token replaced in a template (e.g. <c>{{UserId}}</c>).
/// </summary>
public sealed class VariableDefinition
{
    public required string Name { get; init; }
    public required VariableType Type { get; init; }

    // randomInt
    public int Min { get; init; }
    public int Max { get; init; }

    // randomItem / indexedItem
    public IReadOnlyList<string> Items { get; init; } = Array.Empty<string>();

    // literal
    public string? Value { get; init; }

    // dateTimeNow
    public string Format { get; init; } = "O";

    /// <summary>Produces this variable's value for the given message index.</summary>
    public string Evaluate(int messageIndex, Random random) => Type switch
    {
        VariableType.Literal => Value ?? string.Empty,
        VariableType.RandomInt => random.Next(Min, Max).ToString(),
        VariableType.RandomItem => Items.Count == 0 ? string.Empty : Items[random.Next(Items.Count)],
        VariableType.IndexedItem => Items.Count == 0 ? string.Empty : Items[messageIndex % Items.Count],
        VariableType.DateTimeNow => DateTime.Now.ToString(Format),
        _ => string.Empty,
    };
}
