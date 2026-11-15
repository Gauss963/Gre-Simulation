namespace GreSimulation.Core;

/// <summary>
/// Composition root for the Windows app: loads the bundled content once and opens the saved data.
/// Equivalent to the Mac app's static banks plus <c>UserDefaults.standard</c> and <c>HistoryStore()</c>.
/// </summary>
/// <example>
/// <code>
/// var services = GreServices.Load(Path.Combine(AppContext.BaseDirectory, "Resources"),
///     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Gre Simulation"));
/// var session = services.StartExam(ExamMode.FullLength);
/// </code>
/// </example>
public sealed class GreServices : IDisposable
{
    private GreServices(string resourceDirectory, string? dataDirectory, Random? random)
    {
        ResourceDirectory = resourceDirectory;
        DataDirectory = dataDirectory;
        Settings = new KeyValueStore(dataDirectory);
        Questions = new QuestionBank(resourceDirectory, Settings, random);
        Forms = new FormBuilder(Questions);
        Vocabulary = new VocabularyBank(resourceDirectory);
        Pronunciations = new WordPronunciationLibrary(resourceDirectory);
        History = new HistoryStore(dataDirectory, Settings);
    }

    /// <param name="resourceDirectory">The copied Mac <c>Resources</c> folder.</param>
    /// <param name="dataDirectory">Where Preferences.json and ScoreHistory-v2.json live; null keeps everything in memory.</param>
    /// <param name="random">Optional seeded generator (tests); defaults to <see cref="Random.Shared"/>.</param>
    public static GreServices Load(string resourceDirectory, string? dataDirectory, Random? random = null) =>
        new(resourceDirectory, dataDirectory, random);

    public string ResourceDirectory { get; }
    public string? DataDirectory { get; }
    public KeyValueStore Settings { get; }
    public QuestionBank Questions { get; }
    public FormBuilder Forms { get; }
    public VocabularyBank Vocabulary { get; }
    public WordPronunciationLibrary Pronunciations { get; }
    public HistoryStore History { get; }

    /// <summary>Builds a new form (writes question exposure, like ExamSession(mode:) on the Mac).</summary>
    public ExamSession StartExam(ExamMode mode, TimeProvider? time = null) => new(mode, Forms, time);

    public FlashcardSession CreateFlashcards() => new(Vocabulary.Words, Settings);

    public VocabularyBrowser CreateVocabularyBrowser() => new(Vocabulary);

    /// <summary>The report for a just-finished exam ("Practice test complete" / "Save &amp; Close").</summary>
    public ScoreReport ReportForCompletedExam(ExamResult result) => new(result, Questions);

    /// <summary>The report reopened from Score History ("Saved score report" / "Close").</summary>
    public ScoreReport ReportForSavedResult(ExamResult result) =>
        new(result, Questions, HistoryScreen.SavedReportHeading, HistoryScreen.SavedReportDone);

    /// <summary>All validations (Mac Debug-build launch checks): empty when the content is sound.</summary>
    public IReadOnlyList<string> Validate() =>
        [.. Questions.Validate(), .. Vocabulary.Validate(Pronunciations), .. Forms.Validate()];

    public void Dispose() => Pronunciations.Dispose();
}
