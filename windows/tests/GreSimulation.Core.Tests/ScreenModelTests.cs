using System.Globalization;

namespace GreSimulation.Core.Tests;

public class ScreenModelTests
{
    [Fact]
    public void AxisSpecificationUsesNiceSteps()
    {
        var axis = new AxisSpecification([12, 47, 33], startsAtZero: true);
        Assert.Equal(0, axis.LowerBound);
        Assert.Equal(50, axis.UpperBound);
        Assert.Equal(5, axis.Step);
        Assert.Equal(11, axis.TickValues.Count);

        var fitted = new AxisSpecification([1.2, 1.9], startsAtZero: false);
        Assert.Equal(0.05, fitted.Step, 10);
        Assert.Equal(1.15, fitted.LowerBound, 10); // floor(1.2 / 0.05) is 23 in binary floating point, as in Swift
        Assert.Equal(1.9, fitted.UpperBound, 10);
        Assert.Equal("1.25", fitted.Label(1.25, CultureInfo.InvariantCulture));

        var empty = new AxisSpecification([], startsAtZero: true);
        Assert.Equal(0, empty.LowerBound);
        Assert.Equal(1, empty.UpperBound);
    }

    [Fact]
    public void AxisNumbersRoundLikeFoundation()
    {
        Assert.Equal("1,234", QuantFigureLayout.AxisNumber(1234.4, null, CultureInfo.InvariantCulture));
        Assert.Equal("2", QuantFigureLayout.AxisNumber(2.5, null, CultureInfo.InvariantCulture));
        Assert.Equal("0.5", QuantFigureLayout.AxisNumber(0.5, 0.1, CultureInfo.InvariantCulture));
        Assert.Equal("0.12", QuantFigureLayout.AxisNumber(0.125, 0.025, CultureInfo.InvariantCulture)); // 2 digits, half-even
    }

    [Fact]
    public void EveryBankFigureProducesALayout()
    {
        var figures = TestContent.Services.Questions.All.Select(q => q.Figure).OfType<QuantFigure>().ToList();
        Assert.NotEmpty(figures);
        foreach (var kind in Enum.GetValues<QuantFigureKind>()) Assert.Contains(figures, f => f.Kind == kind);
        foreach (var figure in figures)
        {
            var layout = new QuantFigureLayout(figure);
            Assert.True(layout.MinimumHeight >= 0);
            switch (figure.Kind)
            {
                case QuantFigureKind.Bar or QuantFigureKind.GroupedBar or QuantFigureKind.Histogram:
                    Assert.NotEmpty(layout.CategoryData);
                    Assert.NotNull(layout.YAxis);
                    Assert.All(layout.CategoryData, d => Assert.InRange(d.Value, layout.YAxis!.LowerBound, layout.YAxis.UpperBound));
                    break;
                case QuantFigureKind.Line or QuantFigureKind.Scatter:
                    Assert.NotEmpty(layout.CoordinateData);
                    Assert.NotNull(layout.XAxis);
                    break;
                case QuantFigureKind.Pie:
                    Assert.All(layout.PieCharts, p => Assert.Equal(360, p.Slices.Sum(s => s.SweepDegrees), 6));
                    break;
                case QuantFigureKind.BoxPlot:
                    Assert.NotEmpty(layout.BoxPlotBoxes);
                    Assert.Equal(10, layout.BoxPlotPosition(layout.BoxPlotMinimum, 300), 6);
                    break;
                case QuantFigureKind.Venn:
                    Assert.NotEmpty(layout.VennAnnotations);
                    break;
                case QuantFigureKind.Table:
                    Assert.NotEmpty(layout.TableRows);
                    Assert.StartsWith(string.Join(", ", layout.TableHeaders!), layout.AccessibleTableText);
                    break;
            }
        }
        Assert.True(QuantFigureLayout.IsTrendSeries("Trend line"));
    }

    [Fact]
    public void ScoreReportTextsMatchTheMacFormatting()
    {
        var services = TestContent.Services;
        var session = services.StartExam(ExamMode.VerbalPractice);
        session.CompleteWithCorrectAnswersForTesting();
        var report = services.ReportForCompletedExam(session.Result!);
        Assert.Equal("Practice test complete", report.Heading);
        Assert.Equal("Save & Close", report.DoneTitle);
        var card = Assert.Single(report.ScoreCards);
        Assert.Equal("170", card.ScoreText);
        Assert.Equal("27 of 27 correct", card.RawText);
        Assert.Equal("99th percentile", card.PercentileText);
        Assert.Equal("Verbal Reasoning · Section 2", report.SectionRows[1].Title);
        Assert.Equal("Hard route", report.SectionRows[1].RouteText);
        Assert.Equal("15 / 15", report.SectionRows[1].ScoreText);
        var group = Assert.Single(report.ReviewGroups);
        Assert.Equal("27 / 27 correct", group.CorrectText);
        Assert.All(group.Items, i => Assert.False(string.IsNullOrEmpty(i.GuessingTip)));
        Assert.StartsWith("Section 1 · Question 1", group.Items[0].Heading);
        Assert.Equal(group.Items[0].YourAnswerText.Replace("Your answer: ", ""), group.Items[0].CorrectAnswerText.Replace("Correct answer: ", ""));
        Assert.StartsWith("Verbal Reasoning Measure · ", report.Subtitle(CultureInfo.GetCultureInfo("en-US")));

        var saved = services.ReportForSavedResult(session.Result!);
        Assert.Equal("Saved score report", saved.Heading);
        Assert.Equal("Close", saved.DoneTitle);
    }

    [Fact]
    public void AnswerTexts()
    {
        var numeric = TestContent.Services.Questions.QuantitativeQuestions.First(q => q.Id == "qe-006");
        Assert.Equal("Not answered", ScoreReport.AnswerText(numeric, new GREAnswer()));
        Assert.Equal("3/5", ScoreReport.AnswerText(numeric, new GREAnswer { NumericEntry = "3/5" }));
        Assert.Equal(".6 or 0.6 or 3/5", ScoreReport.CorrectAnswerText(numeric));
        var essay = TestContent.Services.Questions.WritingQuestion;
        Assert.Equal("3-word response", ScoreReport.AnswerText(essay, new GREAnswer { EssayText = "one, two three" }));
        var completion = TestContent.Services.Questions.VerbalQuestions[0];
        Assert.Equal("uncritical", ScoreReport.CorrectAnswerText(completion));
    }

    [Fact]
    public void HistoryAndDashboardTexts()
    {
        Assert.Equal("05:07", HistoryScreen.Duration(307));
        Assert.Equal("1:02:03", HistoryScreen.Duration(3723));
        var result = GreJson.Deserialize<List<ExamResult>>(System.Text.Encoding.UTF8.GetBytes(TestContent.SwiftReference["history.sample"]))![0];
        var card = new HistoryCard(result);
        Assert.Equal("Verbal Reasoning Measure", card.Title);
        Assert.Equal("20:34", card.DurationText);
        Assert.True(card.CanReviewAnswers);
        Assert.Equal(("Quant", "—", "0/0 correct"), card.Metrics[1]);
        Assert.Equal(("Writing", "4.5", "practice signal"), card.Metrics[2]);
        Assert.Equal(("Verbal 1", "5/12", (string?)null), card.SectionChips[0]);
        Assert.Equal([("Verbal", "150"), ("Quant", "—")], Dashboard.LatestScoreChips(result));
        Assert.Equal("Nov 4, 2025", DisplayDates.AbbreviatedDate(new DateTimeOffset(2025, 11, 4, 12, 0, 0, TimeSpan.Zero), CultureInfo.GetCultureInfo("en-US")));
        Assert.Equal("5 sections · 1 hr 58 min · Writing first, then Verbal and Quant in any order", ExamMode.FullLength.Subtitle());
        Assert.Equal("fullLength", ExamMode.FullLength.RawValue());
        Assert.Equal("Practice Tests", AppPage.Dashboard.Title());
        Assert.Equal(3, ResourcesPage.Sections.Count);
    }
}
