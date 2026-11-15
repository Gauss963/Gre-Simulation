using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace GreSimulation.App.Controls;

/// <summary>
/// One row of equally wide children, like a SwiftUI HStack of views with <c>maxWidth: .infinity</c>
/// (the score tiles on the history and resources pages).
/// </summary>
public sealed partial class EqualColumnsPanel : Panel
{
    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing), typeof(double), typeof(EqualColumnsPanel),
        new PropertyMetadata(0d, (d, _) => ((EqualColumnsPanel)d).InvalidateMeasure()));

    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    private int VisibleCount => Children.Count(child => child.Visibility == Visibility.Visible);

    private double ColumnWidth(double totalWidth, int count) =>
        count == 0 ? 0 : Math.Max(0, (totalWidth - Spacing * (count - 1)) / count);

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = VisibleCount;
        double height = 0, widest = 0;
        var width = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : ColumnWidth(availableSize.Width, count);
        foreach (var child in Children)
        {
            if (child.Visibility != Visibility.Visible) continue;
            child.Measure(new Size(width, availableSize.Height));
            height = Math.Max(height, child.DesiredSize.Height);
            widest = Math.Max(widest, child.DesiredSize.Width);
        }
        var totalWidth = double.IsInfinity(availableSize.Width)
            ? widest * count + Spacing * Math.Max(0, count - 1)
            : availableSize.Width;
        return new Size(totalWidth, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var count = VisibleCount;
        var width = ColumnWidth(finalSize.Width, count);
        double x = 0;
        foreach (var child in Children)
        {
            if (child.Visibility != Visibility.Visible)
            {
                child.Arrange(new Rect(0, 0, 0, 0));
                continue;
            }
            child.Arrange(new Rect(x, 0, width, finalSize.Height));
            x += width + Spacing;
        }
        return finalSize;
    }
}
