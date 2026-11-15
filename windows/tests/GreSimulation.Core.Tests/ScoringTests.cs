namespace GreSimulation.Core.Tests;

public class ScoringTests
{
    private static IReadOnlyDictionary<string, string> Swift => TestContent.SwiftReference;

    [Fact]
    public void SelfTestScaleExamples()
    {
        var mid = ScoreEstimator.Scaled(GREMeasure.Verbal, 6, 12, 8, 15, QuestionDifficulty.Medium);
        var low = ScoreEstimator.Scaled(GREMeasure.Verbal, 0, 12, 0, 15, QuestionDifficulty.Easy);
        var hardHigh = ScoreEstimator.Scaled(GREMeasure.Quantitative, 11, 12, 13, 15, QuestionDifficulty.Hard);
        var easyHigh = ScoreEstimator.Scaled(GREMeasure.Quantitative, 11, 12, 13, 15, QuestionDifficulty.Easy);
        Assert.Equal(150, mid);
        Assert.Equal(130, low);
        Assert.Equal(164, hardHigh);
        Assert.Equal(152, easyHigh);
        Assert.Equal(170, ScoreEstimator.Scaled(GREMeasure.Verbal, 12, 12, 15, 15, QuestionDifficulty.Hard));
        Assert.Equal(130, ScoreEstimator.Scaled(GREMeasure.Verbal, 0, 0, 0, 0, null));
    }

    [Theory]
    [InlineData("verbal", "nil")]
    [InlineData("verbal", "easy")]
    [InlineData("verbal", "medium")]
    [InlineData("verbal", "hard")]
    [InlineData("quantitative", "nil")]
    [InlineData("quantitative", "easy")]
    [InlineData("quantitative", "medium")]
    [InlineData("quantitative", "hard")]
    public void EveryRawScoreConvertsLikeTheSwiftEstimator(string measure, string route)
    {
        var expected = Swift[$"scale.{measure}.{route}"].Split(',').Select(int.Parse).ToList();
        var m = measure == "verbal" ? GREMeasure.Verbal : GREMeasure.Quantitative;
        QuestionDifficulty? r = route switch { "easy" => QuestionDifficulty.Easy, "medium" => QuestionDifficulty.Medium, "hard" => QuestionDifficulty.Hard, _ => null };
        var index = 0;
        for (var a = 0; a <= 12; a++)
            for (var b = 0; b <= 15; b++)
                Assert.Equal(expected[index++], ScoreEstimator.Scaled(m, a, 12, b, 15, r));
    }

    [Fact]
    public void Percentiles()
    {
        Assert.Equal(82, ScoreEstimator.Percentile(160, GREMeasure.Verbal));
        Assert.Equal(50, ScoreEstimator.Percentile(160, GREMeasure.Quantitative));
        Assert.Equal(63, ScoreEstimator.WritingPercentile(4.0));
        Assert.Null(ScoreEstimator.Percentile(130, GREMeasure.Verbal));
        Assert.Null(ScoreEstimator.Percentile(132, GREMeasure.Quantitative));
        Assert.Null(ScoreEstimator.WritingPercentile(0));
        Assert.Equal(99, ScoreEstimator.Percentile(170, GREMeasure.Verbal));
        Assert.Equal(89, ScoreEstimator.Percentile(170, GREMeasure.Quantitative));
    }

    [Fact]
    public void NumericAnswerMatching()
    {
        Assert.True(NumericAnswerMatcher.Matches("2.50", "5/2"));
        Assert.False(NumericAnswerMatcher.Matches("2.51", "5/2"));
        foreach (var (key, value) in Swift.Where(p => p.Key.StartsWith("match.")))
        {
            var pair = key["match.".Length..].Split('|');
            Assert.True(bool.Parse(value) == NumericAnswerMatcher.Matches(pair[0], pair[1]), key);
        }
    }

    [Fact]
    public void DoubleParsingMatchesSwift()
    {
        foreach (var (key, value) in Swift.Where(p => p.Key.StartsWith("double.")))
        {
            var text = key["double.".Length..];
            var parsed = SwiftDouble.Parse(text);
            var described = parsed is double d ? (double.IsInfinity(d) ? "inf" : double.IsNaN(d) ? "nan" : SwiftText.Describe(d)) : "nil";
            Assert.True(value == described, $"Double(\"{text}\"): Swift {value}, C# {described}");
        }
    }

    [Fact]
    public void EssayScoresMatchSwift()
    {
        var prompt = TestContent.Services.Questions.WritingQuestion.Prompt;
        string[] essays =
        [
            "",
            prompt + " " + prompt,
            "I like cats and dogs very much and they are great pets for many people in the world today.",
            string.Concat(Enumerable.Repeat("A clear position should examine evidence, acknowledge reasonable limits, and explain why the conclusion follows. ", 35)),
            "I agree that educational institutions should encourage students to question established ideas. For example, students who question ideas learn more deeply.\n\nHowever, critics argue that mastery must come first. Admittedly, some might argue that questioning without knowledge is empty. Nevertheless, I believe students can do both.\n\nFirst, questioning builds understanding. Second, it builds independence. Moreover, history shows that progress depends on challenge. For instance, Galileo questioned established astronomy and transformed science, which demonstrates how institutions benefit.\n\nIn conclusion, schools should encourage questioning established ideas while ensuring students master fundamentals, because thoughtful challenge requires knowledge, and knowledge without challenge stagnates. Therefore educators must balance both goals carefully.",
            string.Concat(Enumerable.Repeat("Students should question established ideas in school because institutions shape thinking; for example, science advances when learners challenge assumptions. However, mastery also matters! Consider medicine: doctors must master anatomy before they question it? Thus balance is essential.\n", 12)),
        ];
        for (var i = 0; i < essays.Length; i++)
        {
            var report = IssueEssayScorer.Score(essays[i], prompt, spellChecker: null);
            var actual = $"{SwiftText.Describe(report.Score)}|{report.RubricRating}|{report.FeatureRating}|{report.Summary}";
            Assert.Equal(Swift[$"essay.{i}"], actual);
        }
    }

    private sealed class FixedSpellChecker(int count) : ISpellChecker
    {
        public int CountMisspellings(string text) => count;
    }

    [Fact]
    public void SpellingErrorsLowerTheEssayScore()
    {
        var prompt = TestContent.Services.Questions.WritingQuestion.Prompt;
        var essay = string.Concat(Enumerable.Repeat("Students should question established ideas in school because institutions shape thinking; for example, science advances when learners challenge assumptions. However, mastery also matters! Consider medicine: doctors must master anatomy before they question it? Thus balance is essential.\n", 12));
        var clean = IssueEssayScorer.Score(essay, prompt, new FixedSpellChecker(0));
        var sloppy = IssueEssayScorer.Score(essay, prompt, new FixedSpellChecker(40));
        Assert.True(sloppy.Score < clean.Score);
        Assert.EndsWith("40 spelling errors.", sloppy.Summary);
    }
}
