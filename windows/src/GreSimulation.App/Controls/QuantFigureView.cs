using GreSimulation.Core;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace GreSimulation.App.Controls;

/// <summary>
/// Port of QuantFigureView.swift: draws a Quantitative figure (table, bar, grouped bar, histogram, line,
/// scatter, pie, box plot, normal curve, Venn) with XAML shapes, using the geometry computed by
/// <see cref="QuantFigureLayout"/>. Charts redraw when resized or when the theme changes.
/// </summary>
public sealed partial class QuantFigureView : UserControl
{
    private readonly QuantFigureLayout _layout;
    private bool _dark;

    public QuantFigureView(QuantFigure figure)
    {
        _layout = new QuantFigureLayout(figure);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        AutomationProperties.SetName(this, figure.Title);
        IsTabStop = false;
        ActualThemeChanged += (_, _) => Build();
        Loaded += (_, _) =>
        {
            if (Palette.IsDark(this) != _dark) Build();
        };
        Build();
    }

    private void Build()
    {
        _dark = Palette.IsDark(this);
        var root = new StackPanel { Spacing = 12 };

        var header = new StackPanel { Spacing = 3 };
        header.Children.Add(Label(_layout.Title, 15, FontWeights.SemiBold, Tone.Ink, wrap: true));
        if (_layout.Caption is { } caption) header.Children.Add(Label(caption, 12, FontWeights.Normal, Tone.Secondary, wrap: true));
        root.Children.Add(header);

        root.Children.Add(BuildBody());

        if (_layout.XAxisTitle is { } xTitle)
        {
            var title = Label(xTitle, 11, FontWeights.SemiBold, Tone.Secondary, wrap: true);
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.TextAlignment = TextAlignment.Center;
            root.Children.Add(title);
        }
        Content = root;
    }

    private UIElement BuildBody() => _layout.Kind switch
    {
        QuantFigureKind.Table => BuildTable(),
        QuantFigureKind.Bar or QuantFigureKind.GroupedBar or QuantFigureKind.Histogram => WithLegend(new DrawingArea(_layout.MinimumHeight, DrawCategoryChart)),
        QuantFigureKind.Line or QuantFigureKind.Scatter or QuantFigureKind.NormalCurve => WithLegend(new DrawingArea(_layout.MinimumHeight, DrawCoordinateChart)),
        QuantFigureKind.Pie => BuildPies(),
        QuantFigureKind.BoxPlot => BuildBoxPlot(),
        _ => BuildVenn(),
    };

    // MARK: - Table

    private UIElement BuildTable()
    {
        var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Left };
        var headers = _layout.TableHeaders;
        var rows = _layout.TableRows;
        var columns = Math.Max(headers?.Count ?? 0, rows.Count == 0 ? 0 : rows.Max(r => r.Count));
        for (var c = 0; c < columns; c++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var rowIndex = 0;
        if (headers is not null)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (var c = 0; c < headers.Count; c++) AddCell(grid, headers[c], 0, c, isHeader: true, Palette.Brush(Tone.Blue, _dark, 0.13));
            rowIndex = 1;
        }
        for (var r = 0; r < rows.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var background = QuantFigureLayout.IsAlternateRow(r) ? Palette.Brush(Tone.Canvas, _dark, 0.8) : Palette.Brush(Tone.Surface, _dark);
            for (var c = 0; c < rows[r].Count; c++) AddCell(grid, rows[r][c], rowIndex + r, c, isHeader: c == 0, background);
        }

        var frame = new Border
        {
            Child = grid,
            BorderBrush = Palette.Brush(Tone.Border, _dark),
            BorderThickness = new Thickness(1, 1, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        var scroller = new ScrollViewer
        {
            Content = frame,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollMode = ScrollMode.Disabled,
        };
        AutomationProperties.SetName(scroller, _layout.AccessibleTableText);
        return scroller;
    }

    private void AddCell(Grid grid, string text, int row, int column, bool isHeader, Brush background)
    {
        var label = Label(text, 13, isHeader ? FontWeights.SemiBold : FontWeights.Normal, Tone.Ink, wrap: false);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        var cell = new Border
        {
            Child = label,
            MinWidth = 82,
            MinHeight = 38,
            Padding = new Thickness(8, 0, 8, 0),
            Background = background,
            BorderBrush = Palette.Brush(Tone.Border, _dark),
            BorderThickness = new Thickness(0, 0, 1, 1),
        };
        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, column);
        grid.Children.Add(cell);
    }

    // MARK: - Bar, grouped bar, histogram

    private void DrawCategoryChart(Canvas canvas, double width, double height)
    {
        var axis = _layout.YAxis;
        if (axis is null) return;
        var data = _layout.CategoryData;
        var categories = _layout.Categories;
        var series = data.Select(d => d.Series).Distinct().ToList();
        var plot = PlotArea(canvas, width, height, axis, showYAxis: true, top: 18, bottom: 22);
        DrawYGrid(canvas, plot, axis, showLabels: true);

        var band = plot.Width / Math.Max(1, categories.Count);
        for (var i = 0; i < categories.Count; i++)
        {
            var items = data.Where(d => d.Label == categories[i]).ToList();
            var bandLeft = plot.X + i * band;
            if (_layout.IsGrouped)
            {
                var groupWidth = band * 0.8;
                var barWidth = groupWidth / Math.Max(1, series.Count);
                foreach (var datum in items)
                {
                    var seriesIndex = series.IndexOf(datum.Series);
                    var left = bandLeft + (band - groupWidth) / 2 + seriesIndex * barWidth;
                    DrawBar(canvas, plot, axis, left + 1, Math.Max(1, barWidth - 2), 0, datum.Value, Palette.Brush(Palette.SeriesTone(seriesIndex), _dark), datum.Value, bold: false);
                }
            }
            else if (_layout.BarsFillCategory)
            {
                var fill = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
                fill.GradientStops.Add(new GradientStop { Color = Palette.ColorOf(Tone.BrightBlue, _dark), Offset = 0 });
                var bottom = Palette.ColorOf(Tone.BrightBlue, _dark);
                bottom.A = 0xC8;
                fill.GradientStops.Add(new GradientStop { Color = bottom, Offset = 1 });
                foreach (var datum in items)
                    DrawBar(canvas, plot, axis, bandLeft, band, 0, datum.Value, fill, datum.Value, bold: true);
            }
            else
            {
                // A plain bar chart stacks several series at one x position (Swift Charts default).
                var barWidth = Math.Min(band * 0.6, 64);
                var left = bandLeft + (band - barWidth) / 2;
                var baseValue = 0.0;
                foreach (var datum in items)
                {
                    var seriesIndex = series.IndexOf(datum.Series);
                    DrawBar(canvas, plot, axis, left, barWidth, baseValue, baseValue + datum.Value, Palette.Brush(Palette.SeriesTone(seriesIndex), _dark), datum.Value, bold: false);
                    baseValue += datum.Value;
                }
            }

            var label = Label(categories[i], 10, FontWeights.Normal, Tone.Secondary, wrap: false);
            label.Width = band;
            label.TextAlignment = TextAlignment.Center;
            label.TextTrimming = TextTrimming.CharacterEllipsis;
            ToolTipService.SetToolTip(label, categories[i]);
            Place(canvas, label, bandLeft, plot.Bottom + 4);
        }
    }

    private void DrawBar(Canvas canvas, PlotRect plot, AxisSpecification axis, double left, double width, double from, double to, Brush fill, double value, bool bold)
    {
        var y0 = plot.Bottom - Math.Clamp(axis.Fraction(from), 0, 1) * plot.Height;
        var y1 = plot.Bottom - Math.Clamp(axis.Fraction(to), 0, 1) * plot.Height;
        var top = Math.Min(y0, y1);
        var rect = new Rectangle { Width = Math.Max(0.5, width), Height = Math.Max(0.5, Math.Abs(y1 - y0)), Fill = fill };
        Place(canvas, rect, left, top);

        var text = Label(QuantFigureLayout.BarValueLabel(value), 10, bold ? FontWeights.Bold : FontWeights.SemiBold, bold ? Tone.Ink : Tone.Secondary, wrap: false);
        var size = Measure(text);
        Place(canvas, text, left + width / 2 - size.Width / 2, top - size.Height - 1);
    }

    // MARK: - Line, scatter, normal curve

    private void DrawCoordinateChart(Canvas canvas, double width, double height)
    {
        var isNormal = _layout.Kind == QuantFigureKind.NormalCurve;
        var yAxis = isNormal ? _layout.NormalCurveYRange : _layout.YAxis;
        var xAxis = _layout.XAxis;
        if (yAxis is null || xAxis is null) return;
        var plot = PlotArea(canvas, width, height, yAxis, showYAxis: !isNormal, top: 10, bottom: 22);
        if (!isNormal) DrawYGrid(canvas, plot, yAxis, showLabels: true);

        // X axis: grid lines and labels at a readable subset of the nice ticks.
        var ticks = xAxis.TickValues;
        var stride = Math.Max(1, (int)Math.Ceiling(ticks.Count / 7.0));
        for (var i = 0; i < ticks.Count; i += stride)
        {
            var x = plot.X + xAxis.Fraction(ticks[i]) * plot.Width;
            canvas.Children.Add(new Line { X1 = x, X2 = x, Y1 = plot.Y, Y2 = plot.Bottom, Stroke = Palette.Brush(Tone.Ink, _dark, 0.12), StrokeThickness = 0.65 });
            canvas.Children.Add(new Line { X1 = x, X2 = x, Y1 = plot.Bottom, Y2 = plot.Bottom + 4, Stroke = Palette.Brush(Tone.Ink, _dark, 0.6), StrokeThickness = 1 });
            var label = Label(xAxis.Label(ticks[i]), 10, FontWeights.Normal, Tone.Secondary, wrap: false);
            var size = Measure(label);
            Place(canvas, label, x - size.Width / 2, plot.Bottom + 5);
        }

        double X(double value) => plot.X + Math.Clamp(xAxis.Fraction(value), 0, 1) * plot.Width;
        double Y(double value) => plot.Bottom - Math.Clamp(yAxis.Fraction(value), 0, 1) * plot.Height;

        var names = _layout.SeriesNames.ToList();
        foreach (var group in _layout.CoordinateData.GroupBy(d => d.Series))
        {
            var index = Math.Max(0, names.IndexOf(group.Key));
            var brush = Palette.Brush(Palette.SeriesTone(index), _dark);
            var points = new PointCollection();
            foreach (var datum in group) points.Add(new Point(X(datum.X), Y(datum.Value)));

            switch (_layout.Kind)
            {
                case QuantFigureKind.Line:
                    canvas.Children.Add(new Polyline { Points = points, Stroke = brush, StrokeThickness = 2.5, StrokeLineJoin = PenLineJoin.Round });
                    foreach (var point in points) DrawDot(canvas, point, 7, brush);
                    break;
                case QuantFigureKind.Scatter when QuantFigureLayout.IsTrendSeries(group.Key):
                    canvas.Children.Add(new Polyline { Points = points, Stroke = brush, StrokeThickness = 2, StrokeDashArray = new DoubleCollection { 3, 2 } });
                    break;
                case QuantFigureKind.Scatter:
                    foreach (var point in points) DrawDot(canvas, point, 9, brush);
                    break;
                default:
                    canvas.Children.Add(new Polyline { Points = points, Stroke = brush, StrokeThickness = 2.5, StrokeLineJoin = PenLineJoin.Round });
                    break;
            }
        }
    }

    private static void DrawDot(Canvas canvas, Point center, double diameter, Brush fill) =>
        Place(canvas, new Ellipse { Width = diameter, Height = diameter, Fill = fill }, center.X - diameter / 2, center.Y - diameter / 2);

    // MARK: - Shared chart frame

    /// <summary>Draws the plot background and returns the plot rectangle (y-axis labels sit on the right, as on the Mac).</summary>
    private PlotRect PlotArea(Canvas canvas, double width, double height, AxisSpecification axis, bool showYAxis, double top, double bottom)
    {
        var right = 8.0;
        if (showYAxis)
        {
            var labelWidth = axis.TickValues.Select(v => Measure(Label(axis.Label(v), 10, FontWeights.Normal, Tone.Secondary, wrap: false)).Width).DefaultIfEmpty(0).Max();
            right = 7 + 4 + labelWidth + 4;
            if (_layout.YAxisTitle.Length > 0) right += 20;
        }
        var plot = new PlotRect(0, top, Math.Max(10, width - right), Math.Max(10, height - top - bottom));
        Place(canvas, new Rectangle
        {
            Width = plot.Width,
            Height = plot.Height,
            Fill = Palette.Brush(Tone.Surface, _dark),
            Stroke = Palette.Brush(Tone.Border, _dark),
            StrokeThickness = 0.7,
        }, plot.X, plot.Y);

        if (showYAxis && _layout.YAxisTitle.Length > 0)
        {
            var title = Label(_layout.YAxisTitle, 11, FontWeights.SemiBold, Tone.Secondary, wrap: false);
            title.MaxWidth = plot.Height;
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            var size = Measure(title);
            var centerX = width - 9;
            var centerY = plot.Y + plot.Height / 2;
            title.RenderTransformOrigin = new Point(0.5, 0.5);
            title.RenderTransform = new RotateTransform { Angle = 90 };
            Place(canvas, title, centerX - size.Width / 2, centerY - size.Height / 2);
        }
        return plot;
    }

    private void DrawYGrid(Canvas canvas, PlotRect plot, AxisSpecification axis, bool showLabels)
    {
        foreach (var tick in axis.TickValues)
        {
            var y = plot.Bottom - axis.Fraction(tick) * plot.Height;
            canvas.Children.Add(new Line { X1 = plot.X, X2 = plot.Right, Y1 = y, Y2 = y, Stroke = Palette.Brush(Tone.Ink, _dark, 0.2), StrokeThickness = 0.65 });
            if (!showLabels) continue;
            canvas.Children.Add(new Line { X1 = plot.Right, X2 = plot.Right + 7, Y1 = y, Y2 = y, Stroke = Palette.Brush(Tone.Ink, _dark, 0.6), StrokeThickness = 1 });
            var label = Label(axis.Label(tick), 10, FontWeights.Normal, Tone.Secondary, wrap: false);
            var size = Measure(label);
            Place(canvas, label, plot.Right + 11, y - size.Height / 2);
        }
    }

    private UIElement WithLegend(UIElement chart)
    {
        if (!_layout.ShowsLegend) return chart;
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(chart);
        var legend = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
        var names = _layout.SeriesNames;
        for (var i = 0; i < names.Count; i++)
        {
            var item = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            item.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = Palette.Brush(Palette.SeriesTone(i), _dark), VerticalAlignment = VerticalAlignment.Center });
            item.Children.Add(Label(names[i], 11, FontWeights.Normal, Tone.Secondary, wrap: false));
            legend.Children.Add(item);
        }
        panel.Children.Add(new ScrollViewer
        {
            Content = legend,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollMode = ScrollMode.Disabled,
        });
        return panel;
    }

    // MARK: - Pie

    private UIElement BuildPies()
    {
        var charts = _layout.PieCharts;
        var items = charts.Select(BuildPie).ToList();
        var grid = new Grid { ColumnSpacing = 20, RowSpacing = 22 };
        foreach (var item in items) grid.Children.Add(item);

        void Arrange(bool horizontal)
        {
            grid.ColumnDefinitions.Clear();
            grid.RowDefinitions.Clear();
            for (var i = 0; i < items.Count; i++)
            {
                if (horizontal) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                else grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetColumn(items[i], horizontal ? i : 0);
                Grid.SetRow(items[i], horizontal ? 0 : i);
            }
        }

        var isHorizontal = true;
        Arrange(true);
        grid.SizeChanged += (_, e) =>
        {
            // ViewThatFits: side by side when every pie gets at least 185 points, otherwise stacked.
            var fits = e.NewSize.Width >= items.Count * 185 + (items.Count - 1) * 20;
            if (fits != isHorizontal)
            {
                isHorizontal = fits;
                Arrange(fits);
            }
        };
        return grid;
    }

    private FrameworkElement BuildPie(PieChart chart)
    {
        var panel = new StackPanel { Spacing = 7, HorizontalAlignment = HorizontalAlignment.Stretch };
        if (chart.ShowsTitle)
        {
            var title = Label(chart.SeriesName, 12, FontWeights.Bold, Tone.Ink, wrap: true);
            title.HorizontalAlignment = HorizontalAlignment.Center;
            panel.Children.Add(title);
        }
        var area = new DrawingArea(_layout.MinimumHeight, (canvas, width, height) => DrawDonut(canvas, width, height, chart)) { MinWidth = 185 };
        AutomationProperties.SetName(area, string.Join(", ", chart.Slices.Select(s => $"{s.Datum.Label}: {QuantFigureLayout.BarValueLabel(s.Datum.Value)}")));
        panel.Children.Add(area);

        var legend = new Grid { ColumnSpacing = 8, RowSpacing = 5 };
        legend.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        legend.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var i = 0; i < chart.Slices.Count; i++)
        {
            if (i % 2 == 0) legend.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var slice = chart.Slices[i];
            var item = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            item.Children.Add(new Ellipse { Width = 7, Height = 7, Fill = Palette.Brush(Palette.PieTone(slice.PaletteIndex), _dark), VerticalAlignment = VerticalAlignment.Center });
            var label = Label(slice.Datum.Label, 11, FontWeights.Normal, Tone.Ink, wrap: false);
            label.TextTrimming = TextTrimming.CharacterEllipsis;
            item.Children.Add(label);
            Grid.SetRow(item, i / 2);
            Grid.SetColumn(item, i % 2);
            legend.Children.Add(item);
        }
        panel.Children.Add(legend);
        return panel;
    }

    private void DrawDonut(Canvas canvas, double width, double height, PieChart chart)
    {
        var outer = Math.Max(4, Math.Min(width, height) / 2 - 2);
        var inner = outer * PieChart.InnerRadiusRatio;
        var center = new Point(width / 2, height / 2);
        foreach (var slice in chart.Slices)
        {
            if (slice.SweepDegrees <= 0) continue;
            var fill = Palette.Brush(Palette.PieTone(slice.PaletteIndex), _dark);
            if (slice.SweepDegrees >= 359.999)
            {
                var ring = new GeometryGroup { FillRule = FillRule.EvenOdd };
                ring.Children.Add(new EllipseGeometry { Center = center, RadiusX = outer, RadiusY = outer });
                ring.Children.Add(new EllipseGeometry { Center = center, RadiusX = inner, RadiusY = inner });
                canvas.Children.Add(new Path { Data = ring, Fill = fill });
                continue;
            }
            // A 1-point gap between slices (angularInset), measured along each edge.
            var outerInset = PieChart.AngularInset / 2 / outer * 180 / Math.PI;
            var innerInset = PieChart.AngularInset / 2 / inner * 180 / Math.PI;
            var start = slice.StartDegrees;
            var end = slice.StartDegrees + slice.SweepDegrees;
            if (end - start <= 2 * innerInset) continue;

            var figure = new PathFigure { StartPoint = OnCircle(center, outer, start + outerInset), IsClosed = true, IsFilled = true };
            figure.Segments.Add(new ArcSegment
            {
                Point = OnCircle(center, outer, end - outerInset),
                Size = new Size(outer, outer),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = end - start - 2 * outerInset > 180,
            });
            figure.Segments.Add(new LineSegment { Point = OnCircle(center, inner, end - innerInset) });
            figure.Segments.Add(new ArcSegment
            {
                Point = OnCircle(center, inner, start + innerInset),
                Size = new Size(inner, inner),
                SweepDirection = SweepDirection.Counterclockwise,
                IsLargeArc = end - start - 2 * innerInset > 180,
            });
            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            canvas.Children.Add(new Path { Data = geometry, Fill = fill });
        }
    }

    /// <summary>A point at <paramref name="degrees"/> clockwise from 12 o'clock.</summary>
    private static Point OnCircle(Point center, double radius, double degrees)
    {
        var radians = (degrees - 90) * Math.PI / 180;
        return new Point(center.X + radius * Math.Cos(radians), center.Y + radius * Math.Sin(radians));
    }

    // MARK: - Box plot

    private UIElement BuildBoxPlot()
    {
        var panel = new StackPanel { Spacing = 10, MinHeight = _layout.MinimumHeight };
        foreach (var box in _layout.BoxPlotBoxes)
        {
            var row = new Grid { ColumnSpacing = 10 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var name = Label(box.Name, 12, FontWeights.Bold, Tone.Ink, wrap: false);
            name.HorizontalAlignment = HorizontalAlignment.Right;
            name.VerticalAlignment = VerticalAlignment.Center;
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            row.Children.Add(name);
            var area = new DrawingArea(48, (canvas, width, height) => DrawBox(canvas, width, height, box));
            AutomationProperties.SetName(area, $"{box.Name}: low {box.Low}, Q1 {box.Q1}, median {box.Median}, Q3 {box.Q3}, high {box.High}");
            Grid.SetColumn(area, 1);
            row.Children.Add(area);
            panel.Children.Add(row);
        }

        var scale = new Grid { Margin = new Thickness(68, 0, 0, 0) };
        scale.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        scale.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        scale.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var minimum = Label(_layout.BoxPlotMinimumLabel(), 11, FontWeights.Normal, Tone.Secondary, wrap: false);
        var caption = Label(QuantFigureLayout.BoxPlotScaleCaption, 11, FontWeights.Normal, Tone.Secondary, wrap: false);
        var maximum = Label(_layout.BoxPlotMaximumLabel(), 11, FontWeights.Normal, Tone.Secondary, wrap: false);
        maximum.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(caption, 1);
        Grid.SetColumn(maximum, 2);
        scale.Children.Add(minimum);
        scale.Children.Add(caption);
        scale.Children.Add(maximum);
        panel.Children.Add(scale);
        return panel;
    }

    private void DrawBox(Canvas canvas, double width, double height, BoxPlotBox box)
    {
        double P(double value) => _layout.BoxPlotPosition(value, width);
        var center = height / 2;
        var navy = Palette.Brush(Tone.Navy, _dark);
        canvas.Children.Add(new Line { X1 = P(box.Low), X2 = P(box.High), Y1 = center, Y2 = center, Stroke = navy, StrokeThickness = 2 });
        foreach (var value in new[] { box.Low, box.High })
            canvas.Children.Add(new Line { X1 = P(value), X2 = P(value), Y1 = center - 11, Y2 = center + 11, Stroke = navy, StrokeThickness = 2 });
        Place(canvas, new Rectangle
        {
            Width = Math.Max(2, P(box.Q3) - P(box.Q1)),
            Height = 32,
            Fill = Palette.Brush(Tone.BrightBlue, _dark, 0.22),
            Stroke = Palette.Brush(Tone.Blue, _dark),
            StrokeThickness = 2,
        }, P(box.Q1), (height - 32) / 2);
        Place(canvas, new Rectangle { Width = 2, Height = 32, Fill = navy }, P(box.Median), (height - 32) / 2);
    }

    // MARK: - Venn

    private UIElement BuildVenn()
    {
        var area = new DrawingArea(_layout.MinimumHeight, DrawVenn);
        AutomationProperties.SetName(area, _layout.VennAccessibilityLabel);
        return area;
    }

    private void DrawVenn(Canvas canvas, double width, double height)
    {
        var diameter = QuantFigureLayout.VennDiameter(width, height);
        void Circle((double X, double Y) center, Tone fill, double opacity, Tone stroke) =>
            Place(canvas, new Ellipse
            {
                Width = diameter,
                Height = diameter,
                Fill = Palette.Brush(fill, _dark, opacity),
                Stroke = Palette.Brush(stroke, _dark),
                StrokeThickness = 2,
            }, center.X * width - diameter / 2, center.Y * height - diameter / 2);

        Circle(QuantFigureLayout.VennLeftCenter, Tone.BrightBlue, 0.2, Tone.Blue);
        Circle(QuantFigureLayout.VennRightCenter, Tone.Teal, 0.18, Tone.Teal);
        foreach (var annotation in _layout.VennAnnotations)
        {
            var stack = new StackPanel { Spacing = 1 };
            var label = Label(annotation.Label, 11, FontWeights.Normal, Tone.Ink, wrap: false);
            var value = Label(annotation.Value, 15, FontWeights.SemiBold, Tone.Ink, wrap: false);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            value.HorizontalAlignment = HorizontalAlignment.Center;
            stack.Children.Add(label);
            stack.Children.Add(value);
            var size = Measure(stack);
            Place(canvas, stack, annotation.X * width - size.Width / 2, annotation.Y * height - size.Height / 2);
        }
    }

    // MARK: - Helpers

    private TextBlock Label(string text, double size, Windows.UI.Text.FontWeight weight, Tone tone, bool wrap) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = weight,
        Foreground = Palette.Brush(tone, _dark),
        TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
    };

    private static Size Measure(FrameworkElement element)
    {
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return element.DesiredSize;
    }

    private static void Place(Canvas canvas, UIElement element, double left, double top)
    {
        Canvas.SetLeft(element, left);
        Canvas.SetTop(element, top);
        canvas.Children.Add(element);
    }

    private readonly record struct PlotRect(double X, double Y, double Width, double Height)
    {
        public double Right => X + Width;
        public double Bottom => Y + Height;
    }

    /// <summary>A fixed-height area whose shapes are redrawn for its current size.</summary>
    private sealed partial class DrawingArea : Grid
    {
        private readonly Canvas _canvas = new();
        private readonly Action<Canvas, double, double> _draw;

        public DrawingArea(double height, Action<Canvas, double, double> draw)
        {
            Height = height;
            _draw = draw;
            HorizontalAlignment = HorizontalAlignment.Stretch;
            Children.Add(_canvas);
            SizeChanged += (_, _) => Redraw();
        }

        private void Redraw()
        {
            _canvas.Children.Clear();
            if (ActualWidth > 0 && ActualHeight > 0) _draw(_canvas, ActualWidth, ActualHeight);
        }
    }
}
