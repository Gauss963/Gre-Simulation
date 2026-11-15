namespace GreSimulation.Core.Tests;

public class VersionTests
{
    [Fact]
    public void VersionComesFromTheMacProject()
    {
        var pbxproj = File.ReadAllText(Path.Combine(RepoPaths.Root, "Gre Simulation.xcodeproj", "project.pbxproj"));
        var match = System.Text.RegularExpressions.Regex.Match(pbxproj, @"MARKETING_VERSION = ([0-9.]+);");
        Assert.True(match.Success);
        Assert.Equal(match.Groups[1].Value, AppInfo.Version);
    }
}

/// <summary>Locates the repository so tests can read the Mac app's bundled resources in place.</summary>
public static class RepoPaths
{
    public static string Root { get; } = Find();
    public static string Resources => Path.Combine(Root, "Gre Simulation", "Resources");

    private static string Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Gre Simulation.xcodeproj"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
