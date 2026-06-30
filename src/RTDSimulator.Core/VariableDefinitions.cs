using Tomlyn;
using Tomlyn.Model;

namespace RTDSimulator.Core;

/// <summary>
/// Loads <see cref="VariableDefinition"/>s from TOML. Each top-level table defines one
/// variable, keyed by its token name, e.g.:
/// <code>
/// [UserId]
/// type = "randomInt"
/// min = 5000
/// max = 5100
/// </code>
/// </summary>
public static class VariableDefinitions
{
    /// <summary>Conventional file name the apps look for next to the executable.</summary>
    public const string DefaultFileName = "variables.toml";

    /// <summary>Parses variable definitions from a TOML string.</summary>
    public static IReadOnlyList<VariableDefinition> Parse(string toml)
    {
        TomlTable model;
        try
        {
            model = Toml.ToModel(toml);
        }
        catch (Exception ex)
        {
            throw new FormatException($"Invalid variable definitions TOML: {ex.Message}", ex);
        }

        var definitions = new List<VariableDefinition>();
        foreach (var entry in model)
        {
            if (entry.Value is not TomlTable table)
            {
                throw new FormatException(
                    $"Variable '{entry.Key}' must be a table, e.g. [{entry.Key}] followed by 'type = ...'.");
            }

            definitions.Add(Build(entry.Key, table));
        }

        return definitions;
    }

    /// <summary>Loads variable definitions from a TOML file on disk.</summary>
    public static IReadOnlyList<VariableDefinition> Load(string path)
        => Parse(File.ReadAllText(path));

    /// <summary>Loads the built-in default definitions embedded in this assembly.</summary>
    public static IReadOnlyList<VariableDefinition> LoadDefaults()
    {
        var assembly = typeof(VariableDefinitions).Assembly;
        string resourceName = assembly.GetManifestResourceNames()
            .Single(n => n.EndsWith("default-variables.toml", StringComparison.OrdinalIgnoreCase));

        using Stream stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    private static VariableDefinition Build(string name, TomlTable table)
    {
        string typeText = GetString(table, "type", name)
            ?? throw new FormatException($"Variable '{name}' is missing required 'type'.");

        VariableType type = typeText.ToLowerInvariant() switch
        {
            "literal" => VariableType.Literal,
            "randomint" => VariableType.RandomInt,
            "randomitem" => VariableType.RandomItem,
            "indexeditem" => VariableType.IndexedItem,
            "datetimenow" => VariableType.DateTimeNow,
            _ => throw new FormatException(
                $"Variable '{name}' has unknown type '{typeText}'. " +
                "Valid types: literal, randomInt, randomItem, indexedItem, dateTimeNow."),
        };

        return type switch
        {
            VariableType.RandomInt => new VariableDefinition
            {
                Name = name,
                Type = type,
                Min = GetInt(table, "min", name),
                Max = GetInt(table, "max", name),
            },
            VariableType.RandomItem or VariableType.IndexedItem => new VariableDefinition
            {
                Name = name,
                Type = type,
                Items = GetItems(table, name),
            },
            VariableType.Literal => new VariableDefinition
            {
                Name = name,
                Type = type,
                Value = GetString(table, "value", name) ?? string.Empty,
            },
            _ => new VariableDefinition // DateTimeNow
            {
                Name = name,
                Type = type,
                Format = GetString(table, "format", name) ?? "O",
            },
        };
    }

    private static string? GetString(TomlTable table, string key, string variableName)
    {
        if (!table.TryGetValue(key, out object? value))
            return null;
        if (value is string s)
            return s;
        throw new FormatException($"Variable '{variableName}': '{key}' must be a string.");
    }

    private static int GetInt(TomlTable table, string key, string variableName)
    {
        if (!table.TryGetValue(key, out object? value))
            throw new FormatException($"Variable '{variableName}' is missing required '{key}'.");
        return value switch
        {
            long l => (int)l,
            int i => i,
            _ => throw new FormatException($"Variable '{variableName}': '{key}' must be an integer."),
        };
    }

    private static IReadOnlyList<string> GetItems(TomlTable table, string variableName)
    {
        if (!table.TryGetValue("items", out object? value) || value is not TomlArray array || array.Count == 0)
            throw new FormatException($"Variable '{variableName}' requires a non-empty 'items' array.");

        return array.Select(item => item?.ToString() ?? string.Empty).ToList();
    }
}
