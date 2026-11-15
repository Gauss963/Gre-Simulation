using System.Reflection;

namespace GreSimulation.Core;

public static class AppInfo
{
    /// <summary>The marketing version shared with the Mac app (for example "1.12.0").</summary>
    public static string Version =>
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split(' ')[0]
        ?? "0.0.0";
}
