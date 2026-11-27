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
        PracticeDrill = new PracticeDrillPool(Questions);
        Vocabulary = new VocabularyBank(resourceDirectory);
        AnswerChoices = new AnswerChoiceWords(resourceDirectory, Vocabulary);
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
    /// <summary>Every Verbal and Quant question for the one-at-a-time practice pages.</summary>
    public PracticeDrillPool PracticeDrill { get; }
    public VocabularyBank Vocabulary { get; }
    /// <summary>Words that appear most often as TC/SE answer choices, in meaning groups.</summary>
    public AnswerChoiceWords AnswerChoices { get; }
    public WordPronunciationLibrary Pronunciations { get; }
    public HistoryStore History { get; }

    /// <summary>Builds a new form (writes question exposure, like ExamSession(mode:) on the Mac).</summary>
    public ExamSession StartExam(ExamMode mode, TimeProvider? time = null) => new(mode, Forms, time);

    public FlashcardSession CreateFlashcards() => new(Vocabulary.Words, Settings, answerChoices: AnswerChoices);

    public VocabularyBrowser CreateVocabularyBrowser() => new(Vocabulary);

    /// <summary>A practice page's state for <paramref name="measure"/>, reading and saving attempts in <see cref="Settings"/>.</summary>
    public PracticeDrillSession CreatePracticeDrill(GREMeasure measure, Random? random = null) =>
        new(new PracticeDrillStore(PracticeDrill, measure, Settings), random);

    /// <summary>The report for a just-finished exam ("Practice test complete" / "Save &amp; Close").</summary>
    public ScoreReport ReportForCompletedExam(ExamResult result) => new(result, Questions);

    /// <summary>The report reopened from Score History ("Saved score report" / "Close").</summary>
    public ScoreReport ReportForSavedResult(ExamResult result) =>
        new(result, Questions, HistoryScreen.SavedReportHeading, HistoryScreen.SavedReportDone);

    /// <summary>All validations (Mac Debug-build launch checks): empty when the content is sound.</summary>
    public IReadOnlyList<string> Validate() =>
        [.. Questions.Validate(), .. Vocabulary.Validate(Pronunciations), .. AnswerChoices.Validate(), .. Forms.Validate()];

    public void Dispose() => Pronunciations.Dispose();
}
