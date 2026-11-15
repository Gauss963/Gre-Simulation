using GreSimulation.Core;
using Microsoft.UI.Dispatching;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;
using Windows.Storage.Streams;

namespace GreSimulation.App.Services;

/// <summary>
/// Port of the Mac app's <c>VocabularySpeechController</c>: plays a word's bundled neural clip
/// (an .m4a slice of WordPronunciations.pack) and falls back to the best installed English system voice
/// when there is no clip or it cannot be played. Toggling the word that is playing stops it.
/// Use it from the UI thread; <see cref="StateChanged"/> is raised on the UI thread.
/// </summary>
public sealed class PronunciationPlayer : IDisposable
{
    /// <summary>The Mac speaks at AVSpeechUtterance rate 0.42 (default 0.5): a little slower than normal.</summary>
    private const double FallbackSpeakingRate = 0.85;

    private readonly WordPronunciationLibrary library;
    private readonly DispatcherQueue dispatcher;
    private MediaPlayer? player;
    private SpeechSynthesizer? synthesizer;
    private int generation;

    public PronunciationPlayer(WordPronunciationLibrary library)
    {
        this.library = library;
        dispatcher = DispatcherQueue.GetForCurrentThread();
    }

    /// <summary>The id of the word being spoken, or null when silent.</summary>
    public string? SpeakingWordId { get; private set; }

    public bool IsSpeaking => SpeakingWordId is not null;

    public bool IsSpeakingWord(VocabularyWord word) => SpeakingWordId == word.Id;

    /// <summary>Raised whenever playback starts or stops.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Plays the word, or stops it when it is already playing (the Listen/Stop button and the V key).</summary>
    public void Toggle(VocabularyWord word)
    {
        if (SpeakingWordId == word.Id)
        {
            Stop();
            return;
        }
        Stop();
        var token = ++generation;
        SpeakingWordId = word.Id;
        StateChanged?.Invoke(this, EventArgs.Empty);
        _ = PlayAsync(word.Word, token);
    }

    /// <summary>Stops any clip or speech immediately.</summary>
    public void Stop()
    {
        generation++;
        ReleasePlayer();
        if (SpeakingWordId is null) return;
        SpeakingWordId = null;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task PlayAsync(string word, int token)
    {
        try
        {
            var clip = library.Clip(word);
            if (clip is not null)
            {
                var stream = await CreateStreamAsync(clip);
                if (token != generation) return;
                Play(MediaSource.CreateFromStream(stream, "audio/mp4"), token, word, isFallback: false);
                return;
            }
            await SpeakAsync(word, token);
        }
        catch (Exception error)
        {
            CrashLog.Write(error);
            Finish(token);
        }
    }

    private async Task SpeakAsync(string word, int token)
    {
        try
        {
            synthesizer ??= CreateSynthesizer();
            var speech = await synthesizer.SynthesizeTextToStreamAsync(word);
            if (token != generation)
            {
                speech.Dispose();
                return;
            }
            Play(MediaSource.CreateFromStream(speech, speech.ContentType), token, word, isFallback: true);
        }
        catch (Exception error)
        {
            CrashLog.Write(error);
            Finish(token);
        }
    }

    private void Play(MediaSource source, int token, string word, bool isFallback)
    {
        ReleasePlayer();
        var mediaPlayer = new MediaPlayer
        {
            AudioCategory = MediaPlayerAudioCategory.Speech,
            AutoPlay = false,
        };
        mediaPlayer.MediaEnded += (_, _) => dispatcher.TryEnqueue(() => Finish(token));
        mediaPlayer.MediaFailed += (_, _) => dispatcher.TryEnqueue(() =>
        {
            if (token != generation) return;
            // Like the Mac: a clip that cannot be played falls back to the system voice.
            if (!isFallback) _ = SpeakAsync(word, token);
            else Finish(token);
        });
        mediaPlayer.Source = source;
        player = mediaPlayer;
        mediaPlayer.Play();
    }

    private void Finish(int token)
    {
        if (token != generation) return;
        ReleasePlayer();
        if (SpeakingWordId is null) return;
        SpeakingWordId = null;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ReleasePlayer()
    {
        if (player is null) return;
        var old = player;
        player = null;
        try
        {
            old.Pause();
            old.Source = null;
            old.Dispose();
        }
        catch (Exception error)
        {
            CrashLog.Write(error);
        }
    }

    private static async Task<IRandomAccessStream> CreateStreamAsync(byte[] bytes)
    {
        var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream))
        {
            writer.WriteBytes(bytes);
            await writer.StoreAsync();
            await writer.FlushAsync();
            writer.DetachStream();
        }
        stream.Seek(0);
        return stream;
    }

    /// <summary>The Mac picks the best installed en-US voice; Windows: en-US, else any English voice, else the default.</summary>
    private static SpeechSynthesizer CreateSynthesizer()
    {
        var synth = new SpeechSynthesizer();
        var voice = BestEnglishVoice();
        if (voice is not null) synth.Voice = voice;
        synth.Options.SpeakingRate = FallbackSpeakingRate;
        return synth;
    }

    private static VoiceInformation? BestEnglishVoice()
    {
        var voices = SpeechSynthesizer.AllVoices;
        var defaultVoice = SpeechSynthesizer.DefaultVoice;
        static bool IsUs(VoiceInformation v) => string.Equals(v.Language, "en-US", StringComparison.OrdinalIgnoreCase);
        static bool IsEnglish(VoiceInformation v) => v.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        // Natural (neural) voices sound closest to the Mac's premium voices.
        static int Quality(VoiceInformation v) => v.DisplayName.Contains("Natural", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

        if (defaultVoice is not null && IsUs(defaultVoice) && Quality(defaultVoice) == 1) return defaultVoice;
        var us = voices.Where(IsUs).OrderByDescending(Quality).FirstOrDefault();
        if (us is not null) return Quality(us) == 0 && defaultVoice is not null && IsUs(defaultVoice) ? defaultVoice : us;
        if (defaultVoice is not null && IsEnglish(defaultVoice)) return defaultVoice;
        return voices.Where(IsEnglish).OrderByDescending(Quality).FirstOrDefault();
    }

    public void Dispose()
    {
        Stop();
        synthesizer?.Dispose();
        synthesizer = null;
    }
}
