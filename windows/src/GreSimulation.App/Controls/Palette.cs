using GreSimulation.Core;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace GreSimulation.App.Controls;

/// <summary>Named colors of the Mac app's GRETheme and the system colors it uses.</summary>
public enum Tone
{
    HeaderNavy,
    ActionBlue,
    Navy,
    Blue,
    BrightBlue,
    Teal,
    Warning,
    /// <summary>Primary text (labelColor).</summary>
    Ink,
    /// <summary>Secondary text (.secondary).</summary>
    Secondary,
    /// <summary>Separator lines (separatorColor).</summary>
    Border,
    /// <summary>Card and plot surface (controlBackgroundColor).</summary>
    Surface,
    /// <summary>Window background (windowBackgroundColor).</summary>
    Canvas,
    /// <summary>Text-entry background (textBackgroundColor).</summary>
    Input,
    Orange,
    Purple,
    Pink,
    Indigo,
    Red,
    Green,
    Cyan,
    Yellow,
}

/// <summary>
/// Theme-aware colors for elements drawn in code (figures, answer choices, the calculator). Brushes are
/// picked for the element's current <see cref="FrameworkElement.ActualTheme"/>; views built in code
/// redraw when the theme changes. XAML uses the matching ThemeResource brushes from Theme.xaml.
/// </summary>
public static class Palette
{
    public static bool IsDark(FrameworkElement element) => element.ActualTheme == ElementTheme.Dark;

    public static Color ColorOf(Tone tone, bool dark) => tone switch
    {
        Tone.HeaderNavy => FromRgb(GreTheme.HeaderNavy),
        Tone.ActionBlue => FromRgb(GreTheme.ActionBlue),
        Tone.Navy => FromRgb(dark ? GreTheme.Navy.Dark : GreTheme.Navy.Light),
        Tone.Blue => FromRgb(dark ? GreTheme.Blue.Dark : GreTheme.Blue.Light),
        Tone.BrightBlue => FromRgb(dark ? GreTheme.BrightBlue.Dark : GreTheme.BrightBlue.Light),
        Tone.Teal => FromRgb(dark ? GreTheme.Teal.Dark : GreTheme.Teal.Light),
        Tone.Warning => FromRgb(dark ? GreTheme.Warning.Dark : GreTheme.Warning.Light),
        Tone.Ink => dark ? Argb(0xFF, 0xFF, 0xFF, 0xFF) : Argb(0xE4, 0x00, 0x00, 0x00),
        Tone.Secondary => dark ? Argb(0xC5, 0xFF, 0xFF, 0xFF) : Argb(0x9E, 0x00, 0x00, 0x00),
        Tone.Border => dark ? Argb(0x33, 0xFF, 0xFF, 0xFF) : Argb(0x26, 0x00, 0x00, 0x00),
        Tone.Surface => dark ? Argb(0xFF, 0x2B, 0x2B, 0x2B) : Argb(0xFF, 0xFF, 0xFF, 0xFF),
        Tone.Canvas => dark ? Argb(0xFF, 0x20, 0x20, 0x20) : Argb(0xFF, 0xF3, 0xF3, 0xF3),
        Tone.Input => dark ? Argb(0xFF, 0x1E, 0x1E, 0x1E) : Argb(0xFF, 0xFF, 0xFF, 0xFF),
        Tone.Orange => dark ? Argb(0xFF, 0xFF, 0x9F, 0x0A) : Argb(0xFF, 0xFF, 0x95, 0x00),
        Tone.Purple => dark ? Argb(0xFF, 0xBF, 0x5A, 0xF2) : Argb(0xFF, 0xAF, 0x52, 0xDE),
        Tone.Pink => dark ? Argb(0xFF, 0xFF, 0x37, 0x5F) : Argb(0xFF, 0xFF, 0x2D, 0x55),
        Tone.Indigo => dark ? Argb(0xFF, 0x5E, 0x5C, 0xE6) : Argb(0xFF, 0x58, 0x56, 0xD6),
        Tone.Red => dark ? Argb(0xFF, 0xFF, 0x45, 0x3A) : Argb(0xFF, 0xFF, 0x3B, 0x30),
        Tone.Green => dark ? Argb(0xFF, 0x30, 0xD1, 0x58) : Argb(0xFF, 0x34, 0xC7, 0x59),
        Tone.Cyan => dark ? Argb(0xFF, 0x64, 0xD2, 0xFF) : Argb(0xFF, 0x32, 0xAD, 0xE6),
        Tone.Yellow => dark ? Argb(0xFF, 0xFF, 0xD6, 0x0A) : Argb(0xFF, 0xFF, 0xCC, 0x00),
        _ => Colors.Gray,
    };

    /// <summary>A brush of <paramref name="tone"/> for <paramref name="element"/>'s theme, optionally translucent.</summary>
    public static SolidColorBrush Brush(FrameworkElement element, Tone tone, double opacity = 1) =>
        Brush(tone, IsDark(element), opacity);

    public static SolidColorBrush Brush(Tone tone, bool dark, double opacity = 1)
    {
        var color = ColorOf(tone, dark);
        if (opacity < 1) color.A = (byte)Math.Round(color.A * Math.Clamp(opacity, 0, 1));
        return new SolidColorBrush(color);
    }

    /// <summary>Swift Charts' default series colors, in order (series i → color i).</summary>
    public static Tone SeriesTone(int index) => SeriesTones[((index % SeriesTones.Length) + SeriesTones.Length) % SeriesTones.Length];

    private static readonly Tone[] SeriesTones = [Tone.BrightBlue, Tone.Green, Tone.Orange, Tone.Purple, Tone.Red, Tone.Cyan, Tone.Yellow];

    /// <summary>Pie slice colors (<see cref="QuantFigureLayout.PiePaletteNames"/>): brightBlue, teal, orange, purple, pink, indigo.</summary>
    public static Tone PieTone(int paletteIndex) => (paletteIndex % 6) switch
    {
        0 => Tone.BrightBlue,
        1 => Tone.Teal,
        2 => Tone.Orange,
        3 => Tone.Purple,
        4 => Tone.Pink,
        _ => Tone.Indigo,
    };

    private static Color FromRgb(GreTheme.Rgb rgb) => Argb(0xFF, rgb.R, rgb.G, rgb.B);

    private static Color Argb(byte a, byte r, byte g, byte b) => ColorHelper.FromArgb(a, r, g, b);
}
