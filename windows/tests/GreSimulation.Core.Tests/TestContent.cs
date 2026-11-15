using System.Text.Json;

namespace GreSimulation.Core.Tests;

/// <summary>Bundled content loaded once for all tests (in-memory settings, no data folder).</summary>
public static class TestContent
{
    private static readonly Lazy<GreServices> Shared = new(() => GreServices.Load(RepoPaths.Resources, dataDirectory: null));

    public static GreServices Services => Shared.Value;

    /// <summary>A fresh in-memory services instance sharing nothing with other tests (slower: reloads JSON).</summary>
    public static GreServices Fresh(string? dataDirectory = null, Random? random = null) =>
        GreServices.Load(RepoPaths.Resources, dataDirectory, random);

    /// <summary>Values produced by the Mac app's Swift code (see Reference/SwiftReference.json).</summary>
    public static IReadOnlyDictionary<string, string> SwiftReference { get; } =
        JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Reference", "SwiftReference.json")))!;

    public static string TempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "gre-core-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
