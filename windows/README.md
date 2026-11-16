# GRE Simulation for Windows

The Windows version of the GRE Simulation app: a WinUI 3 (Windows App SDK) desktop app with the same features, content, version number, and releases as the Mac app. The Mac app in `Gre Simulation/` is the main line; this folder only follows it.

## Use

1. From the [latest release](https://github.com/Gauss963/Gre-Simulation/releases/latest), download `Gre-Simulation-<version>-Windows-Setup.exe`. One installer works on every Windows 10 (19041) / Windows 11 PC; it installs the x64 or ARM64 build to match the machine.
2. Double-click it. The app isn't code-signed, so Windows may show **Windows protected your PC**: choose **More info → Run anyway**. Then click through the installer; no administrator rights are needed.
3. Open **Gre Simulation** from the Start menu (or the desktop shortcut). To update, run a newer Setup.exe over it. To remove it, use **Settings → Apps → Installed apps**.

The app installs to `%LOCALAPPDATA%\Programs\Gre Simulation`. Progress, settings, and score history are saved separately in `%LOCALAPPDATA%\Gre Simulation` (same keys and JSON format as the Mac app), so updating or uninstalling keeps them.

## Layout

- `src/GreSimulation.Core` — platform-neutral logic ported from the Mac app (question bank, form builder, adaptive scoring, calculator, essay scoring, flash cards, saved history). See its `README.md` for the API.
- `src/GreSimulation.App` — the WinUI 3 app (pages, exam screens, calculator, quantitative figures, pronunciation, Windows spell checking).
- `tests/GreSimulation.Core.Tests` — parity tests against values produced by the Mac app's Swift code. They run on macOS too: `dotnet test --project tests/GreSimulation.Core.Tests` from this folder.
- The question bank is the Mac app's `Gre Simulation/Resources`, linked into the build, never copied. The version comes from the Xcode project (`Directory.Build.props`).

## Build

GitHub Actions (`.github/workflows/build-windows-app.yml`) builds x64 and ARM64 on every push, runs the core tests, packs both builds into one Setup.exe with Inno Setup (`installer/Setup.iss`), installs it silently on a Windows runner, runs a smoke test on the installed app that opens every page and practice mode and saves screenshots, and checks that uninstalling keeps saved progress. Pushing a version tag attaches the Setup.exe to that version's release. On Windows you can also build locally with `dotnet publish src/GreSimulation.App -c Release -r win-x64 -p:Platform=x64`.
