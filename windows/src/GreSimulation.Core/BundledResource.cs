using System.Diagnostics;

namespace GreSimulation.Core;

/// <summary>
/// Port of BundledResource.swift: decodes <c>&lt;name&gt;.json</c> from the Resources folder
/// (the Mac app's <c>Gre Simulation/Resources</c>, copied next to the exe as <c>Resources\</c>).
/// Like the Swift version, a missing or undecodable file yields <c>null</c> (and asserts in Debug).
/// </summary>
public static class BundledResource
{
    public static T? Decode<T>(string resourceDirectory, string name) where T : class
    {
        var candidates = new[]
        {
            Path.Combine(resourceDirectory, name + ".json"),
            Path.Combine(resourceDirectory, "Resources", name + ".json"),
        };
        foreach (var path in candidates)
        {
            if (!File.Exists(path)) continue;
            try
            {
                using var stream = File.OpenRead(path);
                return GreJson.Deserialize<T>(stream);
            }
            catch (Exception error) when (error is IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"Could not decode {name}.json: {error.Message}");
                return null;
            }
        }
        Debug.WriteLine($"Missing bundled resource: {name}.json");
        return null;
    }

    /// <summary>Content embedded in this assembly (the Swift-literal question and vocabulary banks).</summary>
    internal static T DecodeEmbedded<T>(string logicalName) where T : class
    {
        using var stream = typeof(BundledResource).Assembly.GetManifestResourceStream(logicalName)
            ?? throw new InvalidOperationException($"Missing embedded resource {logicalName}.");
        return GreJson.Deserialize<T>(stream) ?? throw new InvalidOperationException($"Empty embedded resource {logicalName}.");
    }
}
