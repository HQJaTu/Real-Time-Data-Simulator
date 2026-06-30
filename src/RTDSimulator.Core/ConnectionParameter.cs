namespace RTDSimulator.Core;

/// <summary>
/// Describes a single connection parameter that a connector needs in order to
/// reach its target service. Connectors expose a list of these so that UIs (GUI,
/// CLI) can render the correct fields and labels without hard-coding per-service
/// knowledge.
/// </summary>
public sealed class ConnectionParameter
{
    /// <summary>Stable machine key, used as the CLI <c>--param</c> name and dictionary key.</summary>
    public string Key { get; }

    /// <summary>Human-readable label shown in the UI (the "proper term" for the field).</summary>
    public string Label { get; }

    /// <summary>When true, the value is sensitive and should be masked in UIs.</summary>
    public bool Secret { get; }

    /// <summary>When true, a non-empty value must be supplied.</summary>
    public bool Required { get; }

    /// <summary>Optional pre-filled value.</summary>
    public string? DefaultValue { get; }

    /// <summary>Optional help / hint text.</summary>
    public string? HelpText { get; }

    public ConnectionParameter(
        string key,
        string label,
        bool secret = false,
        bool required = true,
        string? defaultValue = null,
        string? helpText = null)
    {
        Key = key;
        Label = label;
        Secret = secret;
        Required = required;
        DefaultValue = defaultValue;
        HelpText = helpText;
    }
}
