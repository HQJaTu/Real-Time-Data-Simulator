namespace RTDSimulator.Core;

/// <summary>
/// Helper methods exposed to <c>{{$ ... }}</c> template expressions as globals,
/// so they can be called directly, e.g. <c>{{$RandomString(10)}}</c>.
/// </summary>
public sealed class ScriptFunctions
{
    private const string DefaultCharset =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    /// <summary>
    /// Returns a random string of the given <paramref name="length"/>, drawing
    /// characters from <paramref name="charset"/> (alphanumeric by default).
    /// </summary>
    /// <param name="length">Number of characters to generate. Values &lt;= 0 yield an empty string.</param>
    /// <param name="charset">Optional set of characters to choose from. Empty/null uses the default alphanumeric set.</param>
    public string RandomString(int length, string? charset = null)
    {
        if (length <= 0) return string.Empty;
        charset = string.IsNullOrEmpty(charset) ? DefaultCharset : charset;

        var chars = new char[length];
        for (int i = 0; i < length; i++)
            chars[i] = charset[Random.Shared.Next(charset.Length)];
        return new string(chars);
    }
}
