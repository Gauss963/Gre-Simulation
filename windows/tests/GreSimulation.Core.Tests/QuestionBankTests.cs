namespace GreSimulation.Core.Tests;

public class QuestionBankTests
{
    private static QuestionBank Bank => TestContent.Services.Questions;

    [Fact]
    public void ValidateReportsNoIssues() => Assert.Empty(Bank.Validate());

    [Fact]
    public void RetiredQuestionsLoadAndAreNeverInTheBank()
    {
        Assert.Equal(107, Bank.RetiredQuestionReasons.Count);
        Assert.DoesNotContain(Bank.All, q => Bank.RetiredQuestionReasons.ContainsKey(q.Id));
    }

    [Fact]
    public void PoolCountsMatchTheMacApp()
    {
        Assert.Equal(3178, Bank.All.Count(q => q.Measure == GREMeasure.Verbal));
        Assert.Equal(45, Bank.VerbalQuestions.Count);
        Assert.Equal(45, Bank.QuantitativeQuestions.Count);
        // As in Swift, QuestionBank.all keeps the imported Analytical Writing items of the authorized
        // files (only the 2026 additions' Writing items are filtered); forms only draw Verbal and Quant.
        Assert.DoesNotContain(Bank.All, q => q.Measure == GREMeasure.AnalyticalWriting && Bank.Additions2026.Contains(q));
    }

    [Fact]
    public void EveryVerbalQuestionHasAGuessingTipAndARealExplanation()
    {
        string[] boilerplate = ["請依文章中的明示資訊、語意關係", "能使句子的轉折、因果與語氣關係一致", "代入後，句子的文法、轉折、因果與語氣關係一致"];
        foreach (var question in Bank.All.Where(q => q.Measure == GREMeasure.Verbal))
        {
            Assert.False(string.IsNullOrEmpty(Bank.GuessingTip(question)), question.Id);
            Assert.DoesNotContain(boilerplate, b => question.Explanation.Contains(b));
        }
        Assert.Null(Bank.GuessingTip(Bank.QuantitativeQuestions[0]));
    }

    [Fact]
    public void WritingPromptCounts()
    {
        Assert.Equal(70, Bank.WritingQuestions.Count);
        Assert.Equal(58, Bank.WritingQuestions.Count(q => q.Instructions is not null));
        Assert.Equal("aw-001", Bank.WritingQuestions[0].Id);
        Assert.Equal(QuestionFormat.Essay, Bank.WritingQuestion.Format);
    }

    [Fact]
    public void SwiftLiteralQuestionsKeepTheirIdsTextAndKeys()
    {
        var first = Bank.VerbalQuestions[0];
        Assert.Equal("ve-001", first.Id);
        Assert.Equal(QuestionFormat.TextCompletion, first.Format);
        Assert.Equal("Although the biographer admired her subject, she was not _____; the book discusses his failures as candidly as his triumphs.", first.Prompt);
        Assert.Equal(["b0o1"], first.CorrectSelections["blank0"]);
        Assert.Null(first.Groups[0].Title);

        var comparison = Bank.QuantitativeQuestions.First(q => q.Id == "qe-001");
        Assert.Equal(QuestionFormat.QuantitativeComparison, comparison.Format);
        Assert.Equal("x²", comparison.QuantityA);
        Assert.Equal("Compare Quantity A and Quantity B.", comparison.Prompt);
        Assert.Equal(["o3"], comparison.CorrectSelections["main"]);

        var probability = Bank.QuantitativeQuestions.First(q => q.Id == "qe-006");
        Assert.Equal(new HashSet<string> { "0.6", ".6", "3/5" }, probability.AcceptedNumericAnswers);
    }

    [Fact]
    public void GeneratedJsonIsDecodedWithOptionalFields()
    {
        var additions = Bank.Additions2026;
        Assert.Contains(additions, q => q.SetID is not null);
        Assert.Contains(additions, q => q.Format == QuestionFormat.SelectInPassage);
        Assert.Contains(Bank.All, q => q.Figure is { Kind: QuantFigureKind.BoxPlot });
        Assert.All(Bank.All.Where(q => q.Source is not null).Take(50), q => Assert.False(string.IsNullOrEmpty(q.Source!.Title)));
    }

    [Fact]
    public void SetKeyFollowsSetIdThenStimulusThenId()
    {
        var withSet = Bank.Additions2026.First(q => q.SetID is not null);
        Assert.Equal(withSet.SetID, withSet.SetKey);
        var withStimulus = Bank.All.First(q => q.SetID is null && q.Stimulus is not null);
        Assert.Equal("stim:" + withStimulus.Stimulus, withStimulus.SetKey);
        var plain = Bank.All.First(q => q.SetID is null && q.Stimulus is null);
        Assert.Equal(plain.Id, plain.SetKey);
    }

    [Fact]
    public void SourceSummaryCoversTheWholeBank()
    {
        var summary = Bank.SourceSummary;
        Assert.Equal(Bank.All.Count, summary.Sum(s => s.Count));
        Assert.Equal(summary.Select(s => s.Title).Order(StringComparer.Ordinal), summary.Select(s => s.Title));
    }

    [Fact]
    public void FamiliesFollowBalancedQuantIds()
    {
        var question = Bank.BalancedQuantQuestions.First(q => q.Id.Split('-').Length == 4);
        var parts = question.Id.Split('-');
        Assert.Equal($"{parts[0]}-{parts[1]}-{parts[3]}", QuestionBank.SelectionFamily(question));
        Assert.Equal($"{parts[0]}-{parts[1]}", QuestionBank.ScenarioFamily(question));
        Assert.Equal("ve-001", QuestionBank.SelectionFamily(Bank.VerbalQuestions[0]));
    }

    [Fact]
    public void QuestionsForMeasureRemembersExposureAndAvoidsRecent()
    {
        using var services = TestContent.Fresh();
        var bank = services.Questions;
        var first = bank.Questions(GREMeasure.Quantitative, QuestionDifficulty.Hard, 10);
        Assert.Equal(10, first.Count);
        Assert.All(first, q => Assert.Equal(QuestionDifficulty.Hard, q.Difficulty));
        var recent = bank.RecentQuestionIDs(GREMeasure.Quantitative);
        Assert.All(first, q => Assert.Contains(q.Id, recent));
        Assert.NotEmpty(bank.RecentQuestionFamilies(GREMeasure.Quantitative));
        var second = bank.Questions(GREMeasure.Quantitative, QuestionDifficulty.Hard, 10);
        Assert.Empty(first.Select(q => q.Id).Intersect(second.Select(q => q.Id)));

        var mixed = bank.Questions(GREMeasure.Verbal, null, 9);
        Assert.Equal(9, mixed.Count);
        Assert.Equal(3, mixed.Count(q => q.Difficulty == QuestionDifficulty.Easy));
        Assert.Empty(bank.RecentQuestionFamilies(GREMeasure.Verbal));
    }

    [Fact]
    public void ExposureHistoryIsCappedLikeTheMacApp()
    {
        using var services = TestContent.Fresh();
        var bank = services.Questions;
        for (var i = 0; i < 30; i++) bank.Questions(GREMeasure.Quantitative, null, 27);
        var limit = Math.Min(800, Math.Max(120, bank.All.Count(q => q.Measure == GREMeasure.Quantitative) / 2));
        Assert.InRange(services.Settings.GetStringArray(QuestionBank.ExposureKey(GREMeasure.Quantitative))!.Count, 27, limit);
        Assert.Equal(48, services.Settings.GetStringArray(QuestionBank.FamilyExposureKey(GREMeasure.Quantitative))!.Count);
        Assert.Equal("gre-simulation.question-exposure.v1.quantitative", QuestionBank.ExposureKey(GREMeasure.Quantitative));
    }
}
