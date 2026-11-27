namespace GreSimulation.Core.Tests;

public class PracticeDrillTests
{
    [Fact]
    public void PoolsCoverEveryQuestionAndType()
    {
        var services = TestContent.Services;
        var pool = services.PracticeDrill;
        foreach (var measure in new[] { GREMeasure.Verbal, GREMeasure.Quantitative })
        {
            Assert.Equal(services.Questions.All.Count(q => q.Measure == measure), pool.Questions(measure).Count);
            foreach (var type in DrillText.Types(measure))
                Assert.Contains(pool.Questions(measure), q => DrillText.Of(q) == type);
        }
        Assert.Equal(7, DrillText.Types(GREMeasure.Verbal).Count);
        Assert.Equal(4, DrillText.Types(GREMeasure.Quantitative).Count);
        Assert.Equal("textCompletion2", DrillType.TextCompletion2.RawValue());
        Assert.Equal(10, DrillTypeFilter.Menu(GREMeasure.Verbal).Count);
        Assert.Equal(5, DrillTypeFilter.Menu(GREMeasure.Quantitative).Count);
        Assert.Equal(3, DrillTypeFilter.Menu(GREMeasure.Verbal).Single(f => f.Id == "rc").Types!.Count);
        Assert.All(pool.Quantitative, q => Assert.Contains(DrillCategory.QuantAreas, a => a.Id == DrillCategory.QuantArea(q)));
        Assert.Equal(["Arithmetic", "Algebra", "Geometry", "Data Analysis"], DrillCategory.All(GREMeasure.Quantitative).Select(c => c.Id));
        Assert.Equal("gre-simulation.verbal-drill.v1", PracticeDrillStore.StorageKey(GREMeasure.Verbal));
    }

    private static GREAnswer Wrong(GREQuestion question)
    {
        var answer = new GREAnswer();
        foreach (var group in question.Groups)
            answer.Selections[group.Id] = [group.Options.First(o => !question.CorrectSelections[group.Id].Contains(o.Id)).Id];
        return answer;
    }

    private static GREAnswer Right(GREQuestion question) =>
        new() { Selections = question.CorrectSelections.ToDictionary(p => p.Key, p => new HashSet<string>(p.Value)) };

    [Fact]
    public void PassagesContinueAndMissedQuestionsComeBack()
    {
        var services = TestContent.Fresh();
        var store = new PracticeDrillStore(services.PracticeDrill, GREMeasure.Verbal, services.Settings);
        var set = services.PracticeDrill.SetMembers.Values.First(s => s.Count >= 3 && s[0].Measure == GREMeasure.Verbal);
        var random = new Random(1);
        Assert.Equal(set[1].Id, store.Next(set[0], null, null, null, null, false, random)!.Id);

        Assert.False(store.RecordAttempt(set[0], Wrong(set[0])));
        Assert.True(store.RecordAttempt(set[1], Right(set[1])));
        Assert.Equal(set[2].Id, store.Next(set[1], null, null, null, null, false, random)!.Id);
        Assert.Equal(set[0].Id, store.Next(null, null, null, null, null, true, random)!.Id);
        Assert.Equal((2, 1), (store.PracticedCount, store.MissedCount));
        Assert.Equal(0.5, store.RecentAccuracy);
        Assert.Contains(set[0].Id, services.Questions.RecentQuestionIDs(GREMeasure.Verbal));

        var reopened = new PracticeDrillStore(services.PracticeDrill, GREMeasure.Verbal, services.Settings);
        Assert.Equal([set[1].Id, set[0].Id], reopened.History.Select(r => r.QuestionID));
        Assert.Equal(Wrong(set[0]).Selections.Keys, reopened.History[1].Answer.Selections.Keys);
        Assert.Empty(new PracticeDrillStore(services.PracticeDrill, GREMeasure.Quantitative, services.Settings).History);
    }

    [Fact]
    public void FiltersCompletenessAndWeakCategories()
    {
        var services = TestContent.Fresh();
        var store = new PracticeDrillStore(services.PracticeDrill, GREMeasure.Verbal, services.Settings);
        var random = new Random(2);
        var pick = store.Next(null, new HashSet<DrillType> { DrillType.TextCompletion2 }, null, QuestionDifficulty.Hard, null, false, random)!;
        Assert.Equal((DrillType.TextCompletion2, QuestionDifficulty.Hard), (DrillText.Of(pick)!.Value, pick.Difficulty));
        Assert.False(PracticeDrillPool.IsComplete(new GREAnswer { Selections = new() { [pick.Groups[0].Id] = [pick.Groups[0].Options[0].Id] } }, pick));
        Assert.True(PracticeDrillPool.IsComplete(Right(pick), pick));

        var se = services.PracticeDrill.Verbal.First(q => q.Format == QuestionFormat.SentenceEquivalence);
        Assert.False(PracticeDrillPool.IsComplete(new GREAnswer { Selections = new() { ["main"] = [se.Groups[0].Options[0].Id] } }, se));
        Assert.Equal("Select exactly two answer choices.", PracticeDrillPool.CompletionHint(se));

        foreach (var question in services.PracticeDrill.Verbal.Where(q => q.Format == QuestionFormat.SentenceEquivalence).Take(5))
            store.RecordAttempt(question, Wrong(question));
        foreach (var question in services.PracticeDrill.Verbal.Where(q => q.Format == QuestionFormat.TextCompletion).Take(5))
            store.RecordAttempt(question, Right(question));
        Assert.Equal(["sentenceEquivalence", "textCompletion1"], store.WeakCategories.Take(2).Select(c => c.Id));
        var focused = store.Next(null, null, null, null, new HashSet<string> { "sentenceEquivalence" }, false, random)!;
        Assert.Equal(QuestionFormat.SentenceEquivalence, focused.Format);
    }

    [Fact]
    public void QuantNumericEntryAreasAndCalculator()
    {
        var services = TestContent.Fresh();
        var store = new PracticeDrillStore(services.PracticeDrill, GREMeasure.Quantitative, services.Settings);
        var random = new Random(4);
        var geometry = store.Next(null, new HashSet<DrillType> { DrillType.NumericEntry }, "Geometry", null, null, false, random)!;
        Assert.Equal((QuestionFormat.NumericEntry, "Geometry"), (geometry.Format, DrillCategory.QuantArea(geometry)));

        var numeric = services.PracticeDrill.Quantitative.First(q => q.Format == QuestionFormat.NumericEntry && !PracticeDrillPool.IsFractionEntry(q));
        var accepted = numeric.AcceptedNumericAnswers.First();
        Assert.True(store.RecordAttempt(numeric, new GREAnswer { NumericEntry = accepted }));
        Assert.False(store.RecordAttempt(numeric, new GREAnswer { NumericEntry = accepted + "9" }));
        Assert.Equal(accepted + "9", new PracticeDrillStore(services.PracticeDrill, GREMeasure.Quantitative, services.Settings).History[0].Answer.NumericEntry);
        Assert.Equal(DrillCategory.QuantArea(numeric), store.AccuracyByCategory.Single().Category.Id);
        Assert.Contains(numeric.Id, services.Questions.RecentQuestionIDs(GREMeasure.Quantitative));

        var fraction = services.PracticeDrill.Quantitative.First(PracticeDrillPool.IsFractionEntry);
        Assert.False(PracticeDrillPool.IsComplete(new GREAnswer { NumericEntry = "3/" }, fraction));
        Assert.True(PracticeDrillPool.IsComplete(new GREAnswer { NumericEntry = "3/4" }, fraction));
        Assert.Equal("Enter a numerator and a denominator.", PracticeDrillPool.CompletionHint(fraction));

        var session = services.CreatePracticeDrill(GREMeasure.Quantitative, new Random(5));
        Assert.True(session.IsQuant);
        session.Filter = DrillTypeFilter.Menu(GREMeasure.Quantitative).Single(f => f.Types?.Contains(DrillType.NumericEntry) == true);
        session.Area = null;
        while (session.Current is { } q && PracticeDrillPool.IsFractionEntry(q)) session.Next();
        var question = session.Current!;
        session.ShowsCalculator = true;
        foreach (var key in new[] { "1", "2", ".", "5" }) session.Calculator.Input(key);
        Assert.True(session.CanTransferFromCalculator);
        session.TransferCalculatorDisplay();
        Assert.Equal("12.5", session.Answer.NumericEntry);
        session.SetNumericEntryText("x-3.2.1");
        Assert.Equal("-3.21", session.Answer.NumericEntry); // a leading minus, one decimal point
        Assert.True(session.CanCheck);
        session.Check();
        Assert.False(session.CanTransferFromCalculator);
        Assert.Equal(string.Join(" or ", question.AcceptedNumericAnswers.Order(StringComparer.Ordinal)), PracticeDrillPool.CorrectAnswerText(question));
        session.CloseCalculator();
        Assert.False(session.ShowsCalculator);

        var verbal = services.CreatePracticeDrill(GREMeasure.Verbal);
        verbal.ShowsCalculator = true;
        Assert.False(verbal.ShowsCalculator);
    }

    [Fact]
    public void SessionChecksOnceAndMovesOn()
    {
        var services = TestContent.Fresh();
        var session = services.CreatePracticeDrill(GREMeasure.Verbal, new Random(3));
        session.Filter = DrillTypeFilter.Menu(GREMeasure.Verbal).Single(f => f.Id == "se");
        var question = session.Current!;
        Assert.Equal(QuestionFormat.SentenceEquivalence, question.Format);
        Assert.False(session.CanCheck);
        Assert.Equal("Select exactly two answer choices.", session.ActionHint);
        var group = question.Groups[0];
        var key = question.CorrectSelections[group.Id].Order().ToList();
        foreach (var id in key) session.Toggle(group, group.Options.Single(o => o.Id == id));
        session.Toggle(group, group.Options.First(o => !key.Contains(o.Id))); // a third choice is refused
        Assert.True(session.CanCheck);
        session.Check();
        Assert.True(session.IsCorrect);
        Assert.Equal("1 / 1", session.SessionText);
        session.Toggle(group, group.Options[0]); // ignored once checked
        Assert.Equal(2, session.Answer.Selections[group.Id].Count);
        session.TryAgain();
        Assert.Same(question, session.Current);
        Assert.False(session.IsChecked);
        session.Next();
        Assert.NotSame(question, session.Current);
        session.MissedOnly = true;
        Assert.Null(session.Current);
        Assert.Equal("No missed questions", session.EmptyTitle);
    }
}
