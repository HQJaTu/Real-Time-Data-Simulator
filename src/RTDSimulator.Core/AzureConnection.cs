namespace RTDSimulator.Core;

/// <summary>
/// Helpers for working with Azure messaging connection inputs that may be supplied
/// either as a SAS connection string or as a fully-qualified namespace.
/// </summary>
public static class AzureConnection
{
    /// <summary>
    /// Returns the fully-qualified namespace (e.g. <c>my-ns.servicebus.windows.net</c>)
    /// for use with Azure Identity (token-credential) authentication. Accepts either a
    /// bare namespace or a full <c>Endpoint=sb://.../;SharedAccessKey=...</c> connection
    /// string and extracts the host in the latter case.
    /// </summary>
    public static string ResolveNamespace(string connectionStringOrNamespace)
    {
        if (string.IsNullOrWhiteSpace(connectionStringOrNamespace))
            return connectionStringOrNamespace;

        string value = connectionStringOrNamespace.Trim();

        // Full connection string: pull out the Endpoint=sb://host/ portion.
        int schemeIndex = value.IndexOf("sb://", StringComparison.OrdinalIgnoreCase);
        if (schemeIndex >= 0)
        {
            int hostStart = schemeIndex + "sb://".Length;
            int hostEnd = value.IndexOfAny(new[] { '/', ';' }, hostStart);
            string host = hostEnd < 0
                ? value.Substring(hostStart)
                : value.Substring(hostStart, hostEnd - hostStart);
            return host.Trim();
        }

        // Already a bare namespace.
        return value;
    }
}
