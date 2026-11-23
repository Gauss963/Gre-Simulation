namespace GreSimulation.Core.Tests;

public class VerbalDrillTests
{
    [Fact]
    public void PoolCoversEveryVerbalQuestionAndType()
    {
        var services = TestContent.Services;
        var pool = services.VerbalDrill;
        Assert.Equal(services.Questions.All.Count(q => q.Measure == GREMeasure.Verbal), pool.Questions.Count);
        foreach (var type in VerbalDrillText.AllTypes)
            Assert.Contains(pool.Questions, q => VerbalDrillText.Of(q) == type);
        Assert.Equal("textCompletion2", VerbalDrillType.TextCompletion2.RawValue());
        Assert.Equal(10, VerbalDrillFilter.Menu.Count);
        Assert.Equal(3, VerbalDrillFilter.Menu.Single(f => f.Id == "rc").Types!.Count);
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
        var store = new VerbalDrillStore(services.VerbalDrill, services.Settings);
        var set = services.VerbalDrill.SetMembers.Values.First(s => s.Count >= 3);
        var random = new Random(1);
        Assert.Equal(set[1].Id, store.Next(set[0], null, null, null, false, random)!.Id);

        Assert.False(store.RecordAttempt(set[0], Wrong(set[0])));
        Assert.True(store.RecordAttempt(set[1], Right(set[1])));
        Assert.Equal(set[2].Id, store.Next(set[1], null, null, null, false, random)!.Id);
        Assert.Equal(set[0].Id, store.Next(null, null, null, null, true, random)!.Id);
        Assert.Equal((2, 1), (store.PracticedCount, store.MissedCount));
        Assert.Equal(0.5, store.RecentAccuracy);
        Assert.Contains(set[0].Id, services.Questions.RecentQuestionIDs(GREMeasure.Verbal));

        var reopened = new VerbalDrillStore(services.VerbalDrill, services.Settings);
        Assert.Equal([set[1].Id, set[0].Id], reopened.History.Select(r => r.QuestionID));
        Assert.Equal(Wrong(set[0]).Selections.Keys, reopened.History[1].Answer.Selections.Keys);
    }

    [Fact]
    public void FiltersCompletenessAndWeakTypes()
    {
        var services = TestContent.Fresh();
        var store = new VerbalDrillStore(services.VerbalDrill, services.Settings);
        var random = new Random(2);
        var pick = store.Next(null, new HashSet<VerbalDrillType> { VerbalDrillType.TextCompletion2 }, QuestionDifficulty.Hard, null, false, random)!;
        Assert.Equal((VerbalDrillType.TextCompletion2, QuestionDifficulty.Hard), (VerbalDrillText.Of(pick)!.Value, pick.Difficulty));
        Assert.False(VerbalDrillPool.IsComplete(new GREAnswer { Selections = new() { [pick.Groups[0].Id] = [pick.Groups[0].Options[0].Id] } }, pick));
        Assert.True(VerbalDrillPool.IsComplete(Right(pick), pick));

        var se = services.VerbalDrill.Questions.First(q => q.Format == QuestionFormat.SentenceEquivalence);
        Assert.False(VerbalDrillPool.IsComplete(new GREAnswer { Selections = new() { ["main"] = [se.Groups[0].Options[0].Id] } }, se));
        Assert.Equal("Select exactly two answer choices.", VerbalDrillPool.CompletionHint(se));

        foreach (var question in services.VerbalDrill.Questions.Where(q => q.Format == QuestionFormat.SentenceEquivalence).Take(5))
            store.RecordAttempt(question, Wrong(question));
        foreach (var question in services.VerbalDrill.Questions.Where(q => q.Format == QuestionFormat.TextCompletion).Take(5))
            store.RecordAttempt(question, Right(question));
        Assert.Equal([VerbalDrillType.SentenceEquivalence, VerbalDrillType.TextCompletion1], store.WeakTypes.Take(2));
        var focused = store.Next(null, null, null, new HashSet<VerbalDrillType> { VerbalDrillType.SentenceEquivalence }, false, random)!;
        Assert.Equal(QuestionFormat.SentenceEquivalence, focused.Format);
    }

    [Fact]
    public void SessionChecksOnceAndMovesOn()
    {
        var services = TestContent.Fresh();
        var session = services.CreateVerbalDrill(new Random(3));
        session.Filter = VerbalDrillFilter.Menu.Single(f => f.Id == "se");
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
        Assert.StartsWith("Correct answer: ", session.CorrectAnswerText);
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
