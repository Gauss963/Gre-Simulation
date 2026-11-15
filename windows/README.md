# GRE Simulation for Windows

The Windows version of the GRE Simulation app: a WinUI 3 (Windows App SDK) desktop app with the same features, content, version number, and releases as the Mac app. The Mac app in `Gre Simulation/` is the main line; this folder only follows it.

## Use

1. From the [latest release](https://github.com/Gauss963/Gre-Simulation/releases/latest), download `Gre-Simulation-<version>-Windows-x64.zip` (most PCs) or `-Windows-arm64.zip` (Snapdragon / ARM PCs).
2. Unzip it anywhere and run `Gre Simulation\GreSimulation.exe`. Nothing is installed; Windows 10 (19041) or later, Windows 11 recommended.
3. The app is not code-signed, so the first launch may show **Windows protected your PC**. Choose **More info → Run anyway**.

Progress, settings, and score history are saved in `%LOCALAPPDATA%\Gre Simulation` (same keys and JSON format as the Mac app), so replacing the app with a newer zip keeps them.

## Layout

- `src/GreSimulation.Core` — platform-neutral logic ported from the Mac app (question bank, form builder, adaptive scoring, calculator, essay scoring, flash cards, saved history). See its `README.md` for the API.
- `src/GreSimulation.App` — the WinUI 3 app (pages, exam screens, calculator, quantitative figures, pronunciation, Windows spell checking).
- `tests/GreSimulation.Core.Tests` — parity tests against values produced by the Mac app's Swift code. They run on macOS too: `dotnet test --project tests/GreSimulation.Core.Tests` from this folder.
- The question bank is the Mac app's `Gre Simulation/Resources`, linked into the build, never copied. The version comes from the Xcode project (`Directory.Build.props`).

## Build

GitHub Actions (`.github/workflows/build-windows-app.yml`) builds x64 and ARM64 on every push, runs the core tests, and runs a smoke test that opens every page and walks every practice mode with screenshots. Pushing a version tag attaches the zips to that version's release. On Windows you can also build locally with `dotnet publish src/GreSimulation.App -c Release -r win-x64 -p:Platform=x64`.
