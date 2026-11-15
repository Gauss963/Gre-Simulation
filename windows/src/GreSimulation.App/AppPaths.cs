namespace GreSimulation.App;

/// <summary>Where the app reads its bundled content and keeps the learner's saved progress.</summary>
public static class AppPaths
{
    /// <summary>The Mac app's Resources folder, copied next to the executable.</summary>
    public static string ResourcesDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "Resources");

    /// <summary>Saved progress, settings, and history: %LOCALAPPDATA%\Gre Simulation.</summary>
    public static string DataDirectory { get; } = CreateDataDirectory();

    private static string CreateDataDirectory()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Gre Simulation");
        Directory.CreateDirectory(path);
        return path;
    }
}

/// <summary>Writes unexpected errors to crash.log in the data folder so problems can be diagnosed.</summary>
public static class CrashLog
{
    public static void Write(Exception exception)
    {
        try
        {
            File.AppendAllText(Path.Combine(AppPaths.DataDirectory, "crash.log"),
                $"[{DateTime.Now:O}] {exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never take the app down.
        }
    }
}
