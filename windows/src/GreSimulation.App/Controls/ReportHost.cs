using GreSimulation.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GreSimulation.App.Controls;

/// <summary>
/// Opens a saved result's score report on the history page (the Mac's "Saved score report" overlay).
/// Temporary stand-in until the shared score report view is available.
/// </summary>
internal static class ReportHost
{
    public static FrameworkElement Create(ExamResult result, Action close)
    {
        var closeButton = new Button { Content = HistoryScreen.SavedReportDone, Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
        closeButton.Click += (_, _) => close();
        var panel = new StackPanel { Spacing = 12, Margin = new Thickness(36) };
        panel.Children.Add(new TextBlock { Text = HistoryScreen.SavedReportHeading, FontSize = 28, FontWeight = Microsoft.UI.Text.FontWeights.Bold });
        panel.Children.Add(new TextBlock { Text = new HistoryCard(result).Title });
        panel.Children.Add(closeButton);
        return panel;
    }
}
