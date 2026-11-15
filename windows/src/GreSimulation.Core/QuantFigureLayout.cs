using System.Globalization;

namespace GreSimulation.Core;

/// <summary>A labeled value of a bar, histogram, or pie figure (Swift <c>CategoryDatum</c>).</summary>
public sealed record CategoryDatum(string Id, string Series, string Label, double Value, int ColorIndex);

/// <summary>An (x, value) point of a line, scatter, or normal-curve figure (Swift <c>CoordinateDatum</c>).</summary>
public sealed record CoordinateDatum(string Id, string Series, double X, double Value);

/// <summary>A pie slice with angles measured clockwise from 12 o'clock, in degrees (Swift Charts SectorMark).</summary>
public sealed record PieSlice(CategoryDatum Datum, double StartDegrees, double SweepDegrees, int PaletteIndex);

/// <summary>One pie chart (one per series).</summary>
public sealed record PieChart(string SeriesName, bool ShowsTitle, IReadOnlyList<PieSlice> Slices)
{
    /// <summary>Inner radius as a fraction of the outer radius (donut), and the gap between slices in points.</summary>
    public const double InnerRadiusRatio = 0.42;
    public const double AngularInset = 1;
}

/// <summary>A five-number summary row of a box plot.</summary>
public sealed record BoxPlotBox(string Name, double Low, double Q1, double Median, double Q3, double High);

/// <summary>
/// Port of the private <c>YAxisSpecification</c> in QuantFigureView.swift: "nice" bounds, step and ticks
/// (about 15 intervals) for the y axis, labeled on the trailing side.
/// </summary>
public sealed class AxisSpecification
{
    public double LowerBound { get; }
    public double UpperBound { get; }
    public double Step { get; }
    public IReadOnlyList<double> TickValues { get; }

    public AxisSpecification(IEnumerable<double> values, bool startsAtZero)
    {
        var finiteValues = values.Where(double.IsFinite).ToList();
        var dataMinimum = finiteValues.Count > 0 ? finiteValues.Min() : 0;
        var dataMaximum = finiteValues.Count > 0 ? finiteValues.Max() : 1;
        var effectiveMinimum = startsAtZero && dataMinimum >= 0 ? 0 : dataMinimum;
        var span = Math.Max(dataMaximum - effectiveMinimum, Math.Max(Math.Abs(dataMaximum), 1) * 0.05);
        var rawStep = span / 15;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(Math.Max(rawStep, 0.000_001))));
        var normalized = rawStep / magnitude;
        var multiplier = normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 2.5 ? 2.5 : normalized <= 5 ? 5 : 10;
        var resolvedStep = multiplier * magnitude;
        var resolvedLowerBound = startsAtZero && dataMinimum >= 0 ? 0 : Math.Floor(dataMinimum / resolvedStep) * resolvedStep;
        var roundedUpper = Math.Ceiling(dataMaximum / resolvedStep) * resolvedStep;
        var resolvedUpperBound = roundedUpper > resolvedLowerBound ? roundedUpper : resolvedLowerBound + resolvedStep;
        var intervalCount = Math.Max(1, (int)Math.Round((resolvedUpperBound - resolvedLowerBound) / resolvedStep, MidpointRounding.AwayFromZero));
        Step = resolvedStep;
        LowerBound = resolvedLowerBound;
        UpperBound = resolvedUpperBound;
        TickValues = Enumerable.Range(0, intervalCount + 1).Select(i => resolvedLowerBound + i * resolvedStep).ToList();
    }

    public string Label(double value, CultureInfo? culture = null) => QuantFigureLayout.AxisNumber(value, Step, culture);

    /// <summary>Maps a value to a 0–1 fraction of the plot height from the bottom.</summary>
    public double Fraction(double value) => UpperBound > LowerBound ? (value - LowerBound) / (UpperBound - LowerBound) : 0;
}

/// <summary>
/// Everything QuantFigureView.swift computes before drawing, so the Windows UI only draws.
/// Create one per <see cref="QuantFigure"/>; read the members for its <see cref="QuantFigure.Kind"/>.
/// </summary>
public sealed class QuantFigureLayout
{
    public QuantFigureLayout(QuantFigure figure) => Figure = figure;

    public QuantFigure Figure { get; }
    public QuantFigureKind Kind => Figure.Kind;
    public string Title => Figure.Title;
    public string? Caption => Figure.Caption;
    /// <summary>Shown centered under the chart when present.</summary>
    public string? XAxisTitle => Figure.XAxisTitle;
    /// <summary>The y-axis label ("" when absent), as <c>chartYAxisLabel</c>.</summary>
    public string YAxisTitle => Figure.YAxisTitle ?? "";

    /// <summary>Minimum height of the drawing area in points (Mac frame minHeight).</summary>
    public double MinimumHeight => Kind switch
    {
        QuantFigureKind.Scatter or QuantFigureKind.Venn => 285,
        QuantFigureKind.Pie => 210,
        QuantFigureKind.BoxPlot => BoxPlotBoxes.Count * 58 + 34,
        QuantFigureKind.Table => 0,
        _ => 270,
    };

    /// <summary>Legend: bar/grouped bar/line only with more than one series; scatter and normal curve always; others never.</summary>
    public bool ShowsLegend => Kind switch
    {
        QuantFigureKind.Bar or QuantFigureKind.GroupedBar or QuantFigureKind.Line => Figure.Series.Count > 1,
        QuantFigureKind.Scatter or QuantFigureKind.NormalCurve => true,
        _ => false,
    };

    /// <summary>Series names in legend order (each series gets the next chart palette color).</summary>
    public IReadOnlyList<string> SeriesNames => Figure.Series.Select(s => s.Name).ToList();

    // MARK: - Category charts (bar, grouped bar, histogram, pie)

    public IReadOnlyList<CategoryDatum> CategoryData => Figure.Series.SelectMany(CategoryDataFor).ToList();

    public static IReadOnlyList<CategoryDatum> CategoryDataFor(QuantFigureSeries series) =>
        series.Points.Select((point, index) => (point, index))
            .Where(p => p.point.Label is not null && p.point.Value is not null)
            .Select(p => new CategoryDatum($"{series.Name}-{p.index}", series.Name, p.point.Label!, p.point.Value!.Value, p.index))
            .ToList();

    /// <summary>X categories in first-appearance order.</summary>
    public IReadOnlyList<string> Categories => CategoryData.Select(d => d.Label).Distinct().ToList();

    /// <summary>Grouped bars sit side by side per category; plain bars of several series overlap at one position (Mac behavior).</summary>
    public bool IsGrouped => Kind == QuantFigureKind.GroupedBar;

    /// <summary>Histogram bars fill the whole category width (no gaps).</summary>
    public bool BarsFillCategory => Kind == QuantFigureKind.Histogram;

    /// <summary>Value label drawn above each bar (whole numbers, as on the Mac).</summary>
    public static string BarValueLabel(double value, CultureInfo? culture = null) => AxisNumber(value, null, culture);

    // MARK: - Coordinate charts (line, scatter, normal curve)

    public IReadOnlyList<CoordinateDatum> CoordinateData =>
        Figure.Series.SelectMany(series => series.Points.Select((point, index) => (point, index))
            .Where(p => p.point.X is not null && p.point.Value is not null)
            .Select(p => new CoordinateDatum($"{series.Name}-{p.index}", series.Name, p.point.X!.Value, p.point.Value!.Value)))
            .ToList();

    /// <summary>Scatter: a series whose name contains "trend" is drawn as a dashed line (width 2, dash 6/4), others as points (size 70).</summary>
    public static bool IsTrendSeries(string seriesName) => seriesName.Contains("trend", StringComparison.OrdinalIgnoreCase);

    /// <summary>The y axis: bars and histograms start at zero; lines and scatter fit the data. Null for tables, pies, box plots, Venn, and normal curves (hidden axis).</summary>
    public AxisSpecification? YAxis => Kind switch
    {
        QuantFigureKind.Bar or QuantFigureKind.GroupedBar or QuantFigureKind.Histogram => new AxisSpecification(CategoryData.Select(d => d.Value), startsAtZero: true),
        QuantFigureKind.Line or QuantFigureKind.Scatter => new AxisSpecification(CoordinateData.Select(d => d.Value), startsAtZero: false),
        _ => null,
    };

    /// <summary>
    /// Windows-only: an x axis for coordinate charts and a y range for the (axis-less) normal curve. The Mac
    /// uses Swift Charts' automatic scales here, which cannot be reproduced exactly; this uses the same
    /// nice-number rule as the y axis.
    /// </summary>
    public AxisSpecification? XAxis => Kind is QuantFigureKind.Line or QuantFigureKind.Scatter or QuantFigureKind.NormalCurve
        ? new AxisSpecification(CoordinateData.Select(d => d.X), startsAtZero: false)
        : null;

    public AxisSpecification? NormalCurveYRange => Kind == QuantFigureKind.NormalCurve
        ? new AxisSpecification(CoordinateData.Select(d => d.Value), startsAtZero: true)
        : null;

    // MARK: - Pie

    /// <summary>Slice colors by point index: brightBlue, teal, orange, purple, pink, indigo (repeating).</summary>
    public static IReadOnlyList<string> PiePaletteNames { get; } = ["brightBlue", "teal", "orange", "purple", "pink", "indigo"];

    public IReadOnlyList<PieChart> PieCharts => Figure.Series.Select(series =>
    {
        var slices = CategoryDataFor(series);
        var total = slices.Where(s => s.Value > 0).Sum(s => s.Value);
        var start = 0.0;
        var result = new List<PieSlice>();
        foreach (var datum in slices)
        {
            var sweep = total > 0 && datum.Value > 0 ? datum.Value / total * 360 : 0;
            result.Add(new PieSlice(datum, start, sweep, datum.ColorIndex % 6));
            start += sweep;
        }
        return new PieChart(series.Name, Figure.Series.Count > 1, result);
    }).ToList();

    // MARK: - Table

    public IReadOnlyList<string>? TableHeaders => Figure.Headers;
    public IReadOnlyList<IReadOnlyList<string>> TableRows => Figure.Rows ?? [];
    /// <summary>Rows with an even index use the alternate background; column 0 cells use header weight.</summary>
    public static bool IsAlternateRow(int rowIndex) => rowIndex % 2 == 0;

    public string AccessibleTableText
    {
        get
        {
            var header = Figure.Headers is { } h ? string.Join(", ", h) : "";
            var rows = string.Join("; ", (Figure.Rows ?? []).Select(r => string.Join(", ", r)));
            return $"{header}. {rows}";
        }
    }

    // MARK: - Box plot

    public IReadOnlyList<BoxPlotBox> BoxPlotBoxes => Figure.Series
        .Select(s => s.Points.FirstOrDefault() is { Low: double low, Q1: double q1, Median: double median, Q3: double q3, High: double high }
            ? new BoxPlotBox(s.Name, low, q1, median, q3, high)
            : null)
        .OfType<BoxPlotBox>()
        .ToList();

    public double BoxPlotMinimum => BoxPlotBoxes.Count > 0 ? BoxPlotBoxes.Min(b => b.Low) : 0;
    public double BoxPlotMaximum => BoxPlotBoxes.Count > 0 ? BoxPlotBoxes.Max(b => b.High) : 1;

    /// <summary>
    /// Horizontal position of a value in a row of <paramref name="rowWidth"/> points (the area right of the
    /// 58-point name column and 10-point gap). Each row is 48 tall; the box is 32 tall and centered,
    /// whiskers end in 22-point caps; the median is a 2-point line.
    /// </summary>
    public double BoxPlotPosition(double value, double rowWidth)
    {
        var width = Math.Max(1, rowWidth - 20);
        return 10 + width * (value - BoxPlotMinimum) / Math.Max(1, BoxPlotMaximum - BoxPlotMinimum);
    }

    /// <summary>Scale labels under the boxes: minimum, "Five-number summaries", maximum.</summary>
    public string BoxPlotMinimumLabel(CultureInfo? culture = null) => FormatNumber(BoxPlotMinimum, 1, culture, grouping: true);
    public string BoxPlotMaximumLabel(CultureInfo? culture = null) => FormatNumber(BoxPlotMaximum, 1, culture, grouping: true);
    public const string BoxPlotScaleCaption = "Five-number summaries";

    // MARK: - Venn

    /// <summary>Circle diameter for a drawing area of the given size.</summary>
    public static double VennDiameter(double width, double height) => Math.Min(width * 0.48, height * 0.78);
    /// <summary>Circle centers as fractions of the drawing area: (0.4, 0.53) and (0.6, 0.53).</summary>
    public static (double X, double Y) VennLeftCenter => (0.4, 0.53);
    public static (double X, double Y) VennRightCenter => (0.6, 0.53);
    /// <summary>Annotations are centered at (x · width, y · height): label (caption) over value (headline).</summary>
    public IReadOnlyList<QuantFigureAnnotation> VennAnnotations => Figure.Annotations ?? [];
    public string VennAccessibilityLabel => string.Join(", ", VennAnnotations.Select(a => $"{a.Label}: {a.Value}"));

    // MARK: - Number formatting

    /// <summary>
    /// Swift <c>axisNumber(_:step:)</c>: grouping separators and 0…n fraction digits, where n is 0 for a
    /// step ≥ 1 (or no step) and otherwise ceil(−log10 step) clamped to 1…4.
    /// </summary>
    public static string AxisNumber(double value, double? step = null, CultureInfo? culture = null)
    {
        var effectiveStep = step ?? 1;
        var fractionDigits = effectiveStep >= 1 ? 0 : Math.Min(4, Math.Max(1, (int)Math.Ceiling(-Math.Log10(effectiveStep))));
        return FormatNumber(value, fractionDigits, culture, grouping: true);
    }

    /// <summary>Foundation number style with fractionLength(0...max): rounds half-to-even and drops trailing zeros.</summary>
    public static string FormatNumber(double value, int maxFractionDigits, CultureInfo? culture = null, bool grouping = true)
    {
        culture ??= CultureInfo.CurrentCulture;
        var rounded = Math.Round(value, maxFractionDigits, MidpointRounding.ToEven);
        var pattern = (grouping ? "#,##0" : "0") + (maxFractionDigits > 0 ? "." + new string('#', maxFractionDigits) : "");
        return rounded.ToString(pattern, culture);
    }
}
