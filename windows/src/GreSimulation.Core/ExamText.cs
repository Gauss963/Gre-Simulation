namespace GreSimulation.Core;

/// <summary>User-facing strings of the exam screens (ExamView.swift), verbatim from the Mac app.</summary>
public static class ExamText
{
    public static string SectionDirections(GREMeasure measure) => measure switch
    {
        GREMeasure.AnalyticalWriting => "You will have 30 minutes to plan and compose a response to the issue presented. A response to any other issue will receive a score of zero. Make sure that you respond according to the specific instructions and support your position on the issue with reasons and examples drawn from such areas as your reading, experience, observations, and/or academic studies. Trained readers will evaluate how well you respond to the specific task directions, consider the complexities of the issue, organize, develop, and express your ideas, support your ideas with relevant reasons and/or examples, and control the elements of standard written English. The word processor lets you insert and delete text, cut and paste, and undo; spell check and grammar check are not available.",
        GREMeasure.Verbal => "This measure evaluates your ability to analyze written material, understand relationships among words and concepts, and reason from the information provided. Read each question's specific instruction carefully; some questions require more than one answer.",
        _ => "This measure evaluates basic mathematical skills and quantitative reasoning in arithmetic, algebra, geometry, and data analysis. All numbers are real unless otherwise stated. Figures are not necessarily drawn to scale. An on-screen calculator is available.",
    };

    /// <summary>The three bullet points on Verbal/Quant directions screens (not shown for Writing).</summary>
    public static IReadOnlyList<string> SectionRules { get; } =
    [
        "You may skip questions, go back, and change answers within this section; you cannot return to a section after you exit it.",
        "Use Mark and Review to track questions you want to revisit.",
        "Your score is the number of questions answered correctly; nothing is subtracted for incorrect answers, and questions with more than one answer give no partial credit.",
    ];

    public const string TimingBegins = "Timing begins when you select Begin Section.";
    public const string BeginSection = "Begin Section";

    public const string DefaultIssueInstructions = "Write a response in which you discuss the extent to which you agree or disagree with the statement and explain your reasoning for the position you take. In developing and supporting your position, you should consider ways in which the statement might or might not hold true and explain how these considerations shape your position.";

    public const string SelectInPassageHint = "Click a sentence to select it.";

    // Dialogs
    public const string ExitTestTitle = "Exit this test?";
    public const string ExitTestMessage = "Your current test progress will be discarded.";
    public const string ExitTestConfirm = "Exit without saving";
    public const string ExitTestCancel = "Continue test";
    public const string ExitSectionTitle = "Exit this section?";
    public const string ExitSectionConfirm = "Exit Section";
    public const string ExitSectionCancel = "Return";

    // Help sheet
    public const string HelpTitle = "Question Help";
    public const string HelpQuantNote = "All numbers are real unless otherwise stated. Geometric figures are not necessarily drawn to scale; graphical data presentations and coordinate systems are drawn to scale.";
    public const string HelpFootnote = "Help explains the interface and directions only; it does not reveal the answer during a timed section.";

    /// <summary>Quantitative Comparison column titles.</summary>
    public const string QuantityATitle = "Quantity A";
    public const string QuantityBTitle = "Quantity B";
}
