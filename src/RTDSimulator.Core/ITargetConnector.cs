using Azure.Core;

namespace RTDSimulator.Core;

/// <summary>
/// Describes a connectable target service and acts as a factory for
/// <see cref="ITargetConnection"/> instances. Each connector declares the
/// parameters it needs (<see cref="Parameters"/>) so that callers can collect
/// them generically and stay decoupled from service-specific details.
/// </summary>
public interface ITargetConnector
{
    /// <summary>Stable machine key (e.g. <c>"servicebus"</c>), used by the CLI <c>--target</c> option.</summary>
    string Key { get; }

    /// <summary>Human-readable name (e.g. <c>"Azure Service Bus"</c>), shown in the GUI selector.</summary>
    string DisplayName { get; }

    /// <summary>The ordered set of parameters this connector requires.</summary>
    IReadOnlyList<ConnectionParameter> Parameters { get; }

    /// <summary>
    /// Builds a connection from collected parameter values.
    /// </summary>
    /// <param name="values">Values keyed by <see cref="ConnectionParameter.Key"/>.</param>
    /// <param name="credential">
    /// Optional Azure Identity credential. When supplied, connectors that support it
    /// should authenticate with the credential instead of a connection string / key.
    /// </param>
    ITargetConnection CreateConnection(IReadOnlyDictionary<string, string> values, TokenCredential? credential = null);
}
