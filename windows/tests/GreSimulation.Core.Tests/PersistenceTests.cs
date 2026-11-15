using System.Text.Json.Nodes;

namespace GreSimulation.Core.Tests;

public class PersistenceTests
{
    [Fact]
    public void KeyValueStoreRoundTripsThroughItsFile()
    {
        var directory = TestContent.TempDirectory();
        try
        {
            var store = new KeyValueStore(directory);
            store.SetString("gre-simulation.flashcards.mode", "infinite");
            store.SetStringArray("gre-simulation.flashcards.mastered.v1", ["abate", "zeal"]);
            store.SetBool("flag", true);
            store.SetData("blob", [1, 2, 3]);
            store.SetString("gone", "x");
            store.Remove("gone");

            var reopened = new KeyValueStore(directory);
            Assert.Equal("infinite", reopened.GetString("gre-simulation.flashcards.mode"));
            Assert.Equal(["abate", "zeal"], reopened.GetStringArray("gre-simulation.flashcards.mastered.v1"));
            Assert.True(reopened.GetBool("flag"));
            Assert.Equal([1, 2, 3], reopened.GetData("blob"));
            Assert.False(reopened.Contains("gone"));
            Assert.Null(reopened.GetStringArray("gre-simulation.flashcards.mode"));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
            Assert.Equal(["Preferences.json"], Directory.GetFiles(directory).Select(Path.GetFileName));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void KeyValueStoreIsThreadSafe()
    {
        var store = new KeyValueStore(null);
        Parallel.For(0, 200, i => store.SetString($"k{i % 20}", i.ToString()));
        Assert.All(Enumerable.Range(0, 20), i => Assert.NotNull(store.GetString($"k{i}")));
    }

    [Fact]
    public void DamagedPreferencesFileStartsEmpty()
    {
        var directory = TestContent.TempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(directory, KeyValueStore.FileName), "{ not json");
            var store = new KeyValueStore(directory);
            Assert.Null(store.GetString("anything"));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void HistoryReadsTheMacFileFormat()
    {
        var swiftJson = TestContent.SwiftReference["history.sample"];
        var results = GreJson.Deserialize<List<ExamResult>>(System.Text.Encoding.UTF8.GetBytes(swiftJson))!;
        var result = Assert.Single(results);
        Assert.Equal(Guid.Parse("E621E1F8-C36C-495A-93FC-0C247A3E6E5F"), result.Id);
        Assert.Equal(GreJson.ReferenceDate.AddSeconds(784_000_000.25), result.CompletedAt);
        Assert.Equal(ExamMode.VerbalPractice, result.Mode);
        Assert.Equal(150, result.VerbalScore);
        Assert.Null(result.QuantitativeScore);
        Assert.Equal(4.5, result.WritingEstimate);
        Assert.Null(result.WritingSummary);
        Assert.Null(result.Sections[0].Difficulty);
        var review = Assert.Single(result.QuestionReviews!);
        Assert.Equal("verbal-1", review.SectionID);
        Assert.Equal("ve-001", review.Question.Id);
        Assert.Equal(["b0o1"], review.Answer.Selections["blank0"]);
        Assert.True(review.IsCorrect);

        // Re-encoding produces the same JSON tree as Swift's JSONEncoder (key order aside).
        var reencoded = JsonNode.Parse(GreJson.SerializeToUtf8Bytes(results));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(swiftJson), reencoded), reencoded!.ToJsonString());
    }

    [Fact]
    public void HistoryFileRoundTripsAndMigratesLegacyData()
    {
        var directory = TestContent.TempDirectory();
        try
        {
            var settings = new KeyValueStore(directory);
            var legacy = GreJson.Deserialize<List<ExamResult>>(System.Text.Encoding.UTF8.GetBytes(TestContent.SwiftReference["history.sample"]))!;
            settings.SetData(HistoryStore.LegacyStorageKey, GreJson.SerializeToUtf8Bytes(legacy));

            var store = new HistoryStore(directory, settings);
            Assert.Single(store.Results);
            Assert.False(settings.Contains(HistoryStore.LegacyStorageKey));
            Assert.True(File.Exists(Path.Combine(directory, HistoryStore.FileName)));

            var session = TestContent.Services.StartExam(ExamMode.QuantitativePractice);
            session.CompleteWithCorrectAnswersForTesting();
            var notified = 0;
            store.PropertyChanged += (_, _) => notified++;
            store.Add(session.Result!);
            Assert.Equal(1, notified);
            Assert.Equal(session.Result!.Id, store.Results[0].Id);

            var reopened = new HistoryStore(directory, new KeyValueStore(directory));
            Assert.Equal(2, reopened.Results.Count);
            var saved = reopened.Results.First(r => r.Id == session.Result.Id);
            Assert.Equal(170, saved.QuantitativeScore);
            Assert.Equal(27, saved.QuestionReviews!.Count);
            Assert.Equal(session.Result.CompletedAt.ToUnixTimeMilliseconds(), saved.CompletedAt.ToUnixTimeMilliseconds());
            Assert.Equal(reopened.Results.OrderByDescending(r => r.CompletedAt).Select(r => r.Id), reopened.Results.Select(r => r.Id));

            reopened.Clear();
            Assert.Empty(new HistoryStore(directory, new KeyValueStore(directory)).Results);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void HistoryFallsBackToTheKeyValueStoreWithoutADataDirectory()
    {
        var settings = new KeyValueStore(null);
        var store = new HistoryStore(null, settings);
        var session = TestContent.Services.StartExam(ExamMode.VerbalPractice);
        session.CompleteWithCorrectAnswersForTesting();
        store.Add(session.Result!);
        Assert.NotNull(settings.GetData(HistoryStore.LegacyStorageKey));
        Assert.Single(new HistoryStore(null, settings).Results);
    }
}
