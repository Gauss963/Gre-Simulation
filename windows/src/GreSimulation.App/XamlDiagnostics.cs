using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml.Markup;

namespace GreSimulation.App;

/// <summary>
/// When compiled XAML fails to load, WinUI only says "XAML parsing failed". This reloads the
/// embedded source of each XAML file with XamlReader, whose errors name the line and the cause,
/// and writes them to crash.log.
/// </summary>
internal static class XamlDiagnostics
{
    public static void Report()
    {
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.EndsWith(".xaml.src", StringComparison.Ordinal)))
        {
            using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
            var text = reader.ReadToEnd();
            // Code-behind hooks (x:Class, event handlers) cannot be loaded loosely; drop them.
            text = Regex.Replace(text, @"\sx:Class=""[^""]*""", "");
            text = Regex.Replace(text, @"\s(Click|SelectionChanged|Loaded|Unloaded|Tapped|TextChanged|KeyDown|PointerPressed|Checked|Unchecked|Toggled|ValueChanged|QuerySubmitted|ItemClick|SizeChanged|Closed|Opened|DoubleTapped|PreviewKeyDown|GotFocus|LostFocus|SelectorBar\.SelectionChanged)=""[^""]*""", "");
            text = Regex.Replace(text, @"\{x:Bind[^}]*\}", "");
            // A Window is not loadable by XamlReader; test its content instead.
            if (Regex.IsMatch(text, @"<Window\b"))
            {
                var inner = Regex.Match(text, @"<Window\b[^>]*>(?<body>.*)</Window>", RegexOptions.Singleline).Groups["body"].Value;
                inner = Regex.Replace(inner, @"<Window\.SystemBackdrop>.*?</Window\.SystemBackdrop>", "", RegexOptions.Singleline);
                text = "<Grid xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">" + inner + "</Grid>";
            }
            try
            {
                XamlReader.Load(text);
                CrashLog.Write(new InvalidOperationException($"XAML check ok: {name}"));
            }
            catch (Exception exception)
            {
                CrashLog.Write(new InvalidOperationException($"XAML check FAILED: {name}: {exception.Message}"));
            }
        }
    }
}
