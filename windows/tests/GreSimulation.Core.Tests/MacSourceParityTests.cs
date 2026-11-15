using System.Text.RegularExpressions;

namespace GreSimulation.Core.Tests;

/// <summary>
/// Guards the embedded copies of Swift-literal content against drift: if the Mac sources change,
/// run Reference/SwiftExport/export.sh to regenerate them.
/// </summary>
public class MacSourceParityTests
{
    private static string MacSource(string file) => File.ReadAllText(Path.Combine(RepoPaths.Root, "Gre Simulation", file));

    [Fact]
    public void EmbeddedLiteralQuestionsMatchTheSwiftFiles()
    {
        var pattern = new Regex(@"\b(?:single|multiple|completion|equivalence|numeric|comparison)\(""([a-z]{2}-\d+)""");
        var verbalIds = pattern.Matches(MacSource("VerbalQuestionBank.swift")).Select(m => m.Groups[1].Value).ToList();
        var quantIds = pattern.Matches(MacSource("QuantitativeQuestionBank.swift")).Select(m => m.Groups[1].Value).ToList();
        var bank = TestContent.Services.Questions;
        Assert.Equal(verbalIds, bank.VerbalQuestions.Select(q => q.Id));
        Assert.Equal(quantIds, bank.QuantitativeQuestions.Select(q => q.Id));
        Assert.Contains($"\"{bank.WritingQuestion.Prompt}\"", MacSource("QuestionBank.swift"));
        foreach (var question in bank.VerbalQuestions.Concat(bank.QuantitativeQuestions).Where(q => q.Format != QuestionFormat.QuantitativeComparison && !q.Prompt.Contains('"') && !q.Prompt.Contains('\\')))
            Assert.Contains(question.Prompt, MacSource(question.Measure == GREMeasure.Verbal ? "VerbalQuestionBank.swift" : "QuantitativeQuestionBank.swift"));
    }

    [Fact]
    public void EmbeddedCuratedVocabularyMatchesTheSwiftFile()
    {
        var words = Regex.Matches(MacSource("VocabularyBank.swift"), @"^\s*word\(""([^""]+)"", ""([^""]*)"", ""([^""]*)""", RegexOptions.Multiline)
            .Select(m => (m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value)).ToList();
        var curated = TestContent.Services.Vocabulary.CuratedWords;
        Assert.Equal(words, curated.Select(w => (w.Word, w.Pronunciation, w.PartOfSpeech ?? "")));
        Assert.All(curated, w => Assert.Equal(["Original app notes"], w.Sources));
    }

    [Fact]
    public void PercentileAndScaleTablesMatchTheSwiftFile()
    {
        var source = MacSource("ExamSession.swift");
        Assert.Contains("(0, 130), (2, 130), (5, 136), (10, 144), (15, 149), (20, 154), (25, 159), (30, 165), (35, 170)", source);
        Assert.Contains("(0, 130), (2, 130), (5, 134), (10, 141), (15, 146), (20, 150), (25, 155), (30, 162), (35, 170)", source);
        foreach (Match m in Regex.Match(source, @"let verbal = \[(.*?)\]").Groups[1].Value is var v ? Regex.Matches(v, @"(\d+): (\d+)") : null!)
            Assert.Equal(int.Parse(m.Groups[2].Value), ScoreEstimator.Percentile(int.Parse(m.Groups[1].Value), GREMeasure.Verbal));
        foreach (Match m in Regex.Match(source, @"let quant = \[(.*?)\]").Groups[1].Value is var q ? Regex.Matches(q, @"(\d+): (\d+)") : null!)
            Assert.Equal(int.Parse(m.Groups[2].Value), ScoreEstimator.Percentile(int.Parse(m.Groups[1].Value), GREMeasure.Quantitative));
    }
}
