using GreSimulation.Core;

namespace GreSimulation.App.Services;

/// <summary>
/// The app-wide <see cref="GreServices"/> (question bank, forms, vocabulary, pronunciations, history,
/// settings), loaded once off the UI thread when the app starts.
/// </summary>
/// <remarks>
/// <para>Usage from a page:</para>
/// <code>
/// var services = await AppServices.Ready;      // safe at any time; completes when loading has finished
/// if (AppServices.IsReady) Use(AppServices.Services);   // synchronous access once loaded
/// </code>
/// <list type="bullet">
/// <item><see cref="Ready"/> — a task that completes with the loaded services (faults if loading failed).</item>
/// <item><see cref="Services"/> (alias <see cref="Current"/>) — the loaded services; throws before <see cref="Ready"/> has completed.</item>
/// <item><see cref="IsReady"/> — true once loading has finished successfully.</item>
/// </list>
/// <para>
/// <see cref="Start"/> is called from <c>App.OnLaunched</c>; it is idempotent, and reading <see cref="Ready"/>
/// also starts loading. Loading assigns <see cref="IssueEssayScorer.SpellChecker"/> a Windows
/// spell checker (<see cref="WindowsSpellChecker"/>), so essay estimates count spelling errors like the Mac app.
/// Every <see cref="GreServices"/> change is saved immediately; <see cref="Shutdown"/> only releases the audio pack.
/// </para>
/// </remarks>
public static class AppServices
{
    private static readonly object Gate = new();
    private static Task<GreServices>? _ready;

    /// <summary>Completes with the loaded services. Await it before first use (for example on a loading page).</summary>
    public static Task<GreServices> Ready
    {
        get
        {
            Start();
            return _ready!;
        }
    }

    /// <summary>True once the services have loaded successfully.</summary>
    public static bool IsReady => _ready is { IsCompletedSuccessfully: true };

    /// <summary>The loaded services. Only valid when <see cref="IsReady"/>; otherwise await <see cref="Ready"/>.</summary>
    public static GreServices Services => IsReady
        ? _ready!.Result
        : throw new InvalidOperationException("AppServices is still loading; await AppServices.Ready first.");

    /// <summary>Alias of <see cref="Services"/>.</summary>
    public static GreServices Current => Services;

    /// <summary>Starts loading the content on a background thread (does nothing when already started).</summary>
    public static void Start()
    {
        lock (Gate)
        {
            _ready ??= Task.Run(Load);
        }
    }

    /// <summary>Releases the services (the pronunciation audio pack). Call when the app exits.</summary>
    public static void Shutdown()
    {
        if (IsReady) _ready!.Result.Dispose();
    }

    private static GreServices Load()
    {
        try
        {
            var services = GreServices.Load(AppPaths.ResourcesDirectory, AppPaths.DataDirectory);
            IssueEssayScorer.SpellChecker = WindowsSpellChecker.TryCreate();
            return services;
        }
        catch (Exception exception)
        {
            CrashLog.Write(exception);
            throw;
        }
    }
}
