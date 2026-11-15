using GreSimulation.App.Controls;
using GreSimulation.App.Exam;
using GreSimulation.App.Services;
using GreSimulation.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace GreSimulation.App;

/// <summary>
/// Launch with <c>--smoke-test &lt;folder&gt;</c> (used by CI): opens every sidebar page and walks
/// every practice mode through all sections and questions with sample answers, saving screenshots
/// and a log to the folder. The process exits 0 when nothing threw, 1 otherwise. Nothing is saved
/// to the learner's history.
/// </summary>
internal static class SmokeTest
{
    public static string? OutputDirectory { get; private set; }
    private static readonly List<string> Log = [];
    private static int failures;

    public static bool Enabled => OutputDirectory is not null;

    public static void Configure(string[] args)
    {
        var index = Array.IndexOf(args, "--smoke-test");
        if (index >= 0 && index + 1 < args.Length) OutputDirectory = Path.GetFullPath(args[index + 1]);
    }

    public static void RecordFailure(string where, Exception exception)
    {
        failures++;
        Log.Add($"FAIL {where}: {exception}");
    }

    public static async Task RunAsync(MainWindow window)
    {
        Directory.CreateDirectory(OutputDirectory!);
        var shot = 0;
        async Task Step(string name, Action action)
        {
            try
            {
                action();
                await Task.Delay(700);
                await Capture(window, $"{++shot:000}-{name}");
                Log.Add($"ok   {name}");
            }
            catch (Exception exception)
            {
                RecordFailure(name, exception);
            }
        }

        try
        {
            var services = await AppServices.Ready;
            await Task.Delay(1500);
            await Step("dashboard", () => { });
            foreach (var tag in new[] { "vocabulary", "history", "resources", "dashboard" })
                await Step($"page-{tag}", () => window.Navigate(tag));

            foreach (var mode in Enum.GetValues<ExamMode>())
            {
                var session = services.StartExam(mode);
                await Step($"{mode}-start", () => window.ShowExam(new ExamView(session, services)));
                var guard = 0;
                while (session.Phase != ExamPhase.Completed && guard++ < 12)
                {
                    var label = $"{mode}-s{session.CurrentSectionIndex + 1}";
                    await Step($"{label}-directions", () => { });
                    await Step($"{label}-begin", session.BeginSection);
                    var count = session.CurrentSection.Questions.Count;
                    for (var i = 0; i < count; i++)
                    {
                        var question = session.CurrentQuestion;
                        await Step($"{label}-q{i + 1:00}-{question.Format}", () => Answer(session, question));
                        if (session.CurrentSection.Measure == GREMeasure.Quantitative && i == 0)
                            await Step($"{label}-calculator", () => session.IsCalculatorPresented = true);
                        if (i < count - 1) session.GoNext();
                    }
                    await Step($"{label}-finish", session.ConfirmExitSection);
                }
                await Step($"{mode}-report", () => { });
                if (session.Result is { } result)
                    await Step($"{mode}-saved-report", () => window.ShowExam(new ScoreReportView(result)));
                window.CloseExam();
            }
        }
        catch (Exception exception)
        {
            RecordFailure("run", exception);
        }

        Log.Add(failures == 0 ? "SMOKE TEST PASSED" : $"SMOKE TEST FAILED ({failures})");
        await File.WriteAllLinesAsync(Path.Combine(OutputDirectory!, "smoke.log"), Log);
        Environment.Exit(failures == 0 ? 0 : 1);
    }

    /// <summary>Gives every question a sample answer so the answered states render too.</summary>
    private static void Answer(ExamSession session, GREQuestion question)
    {
        switch (question.Format)
        {
            case QuestionFormat.NumericEntry:
                if (question.AcceptedNumericAnswers.Any(a => a.Contains('/')))
                {
                    session.SetFractionPart(0, "1");
                    session.SetFractionPart(1, "2");
                }
                else
                {
                    session.SetNumericEntryText("12.5");
                }
                break;
            case QuestionFormat.Essay:
                session.UpdateEssay("Sample response. " + string.Concat(Enumerable.Repeat("This sentence adds a reason and an example. ", 40)));
                break;
            default:
                foreach (var group in question.Groups)
                    session.ToggleSelection(group.Id, group.Options[0].Id, group.MaximumSelections);
                break;
        }
    }

    private static async Task Capture(MainWindow window, string name)
    {
        if (window.Content is not UIElement root) return;
        var bitmap = new RenderTargetBitmap();
        await bitmap.RenderAsync(root);
        var pixels = await bitmap.GetPixelsAsync();
        var path = Path.Combine(OutputDirectory!, name + ".png");
        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels.ToArray());
        await encoder.FlushAsync();
        stream.Seek(0);
        var bytes = new byte[stream.Size];
        await stream.ReadAsync(bytes.AsBuffer(), (uint)stream.Size, InputStreamOptions.None);
        await File.WriteAllBytesAsync(path, bytes);
    }
}
