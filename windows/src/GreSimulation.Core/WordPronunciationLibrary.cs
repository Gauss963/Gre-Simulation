using System.Text.Json.Serialization;
using Microsoft.Win32.SafeHandles;

namespace GreSimulation.Core;

/// <summary>
/// Port of <c>WordPronunciationLibrary</c> (WordPronunciation.swift): pre-rendered neural word clips
/// (Kokoro-82M, Apache-2.0) bundled as one pack. <c>WordPronunciations.json</c> maps each lower-cased
/// word to <c>[offset, length]</c> in <c>WordPronunciations.pack</c>; each slice is a complete .m4a file.
/// The pack is never loaded whole: clips are read on demand with positional reads (thread-safe).
/// The App plays the returned bytes (for example with MediaPlayer + an InMemoryRandomAccessStream);
/// when <see cref="Clip"/> returns null the Mac app falls back to the system speech voice
/// (rate 0.42 of normal, en-US) — the App should do the same with Windows speech synthesis.
/// </summary>
public sealed class WordPronunciationLibrary : IDisposable
{
    public const string IndexFileName = "WordPronunciations.json";
    public const string PackFileName = "WordPronunciations.pack";

    private sealed class Index
    {
        [JsonPropertyName("clips")] public Dictionary<string, int[]>? Clips { get; init; }
    }

    private readonly IReadOnlyDictionary<string, int[]> _clips;
    private readonly SafeFileHandle? _pack;
    private readonly long _packLength;

    public WordPronunciationLibrary(string resourceDirectory)
    {
        var indexPath = Path.Combine(resourceDirectory, IndexFileName);
        var packPath = Path.Combine(resourceDirectory, PackFileName);
        Dictionary<string, int[]>? clips = null;
        try
        {
            if (File.Exists(indexPath))
            {
                using var stream = File.OpenRead(indexPath);
                clips = GreJson.Deserialize<Index>(stream)?.Clips;
            }
        }
        catch (Exception error) when (error is IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            clips = null;
        }
        _clips = clips ?? new Dictionary<string, int[]>();
        try
        {
            if (File.Exists(packPath))
            {
                _pack = File.OpenHandle(packPath, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.RandomAccess);
                _packLength = RandomAccess.GetLength(_pack);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            _pack = null;
        }
    }

    public int ClipCount => _clips.Count;

    /// <summary>The m4a bytes for <paramref name="word"/>, or null when there is no valid clip.</summary>
    public byte[]? Clip(string word)
    {
        if (!TryRange(word, out var offset, out var length) || _pack is null) return null;
        var buffer = new byte[length];
        var read = 0;
        while (read < length)
        {
            var n = RandomAccess.Read(_pack, buffer.AsSpan(read), offset + read);
            if (n <= 0) return null;
            read += n;
        }
        return buffer;
    }

    public bool HasClip(string word) => TryRange(word, out _, out _);

    private bool TryRange(string word, out long offset, out int length)
    {
        offset = 0;
        length = 0;
        if (_pack is null || !_clips.TryGetValue(Key(word), out var range) || range.Length != 2) return false;
        if (range[0] < 0 || range[1] <= 0 || (long)range[0] + range[1] > _packLength) return false;
        offset = range[0];
        length = range[1];
        return true;
    }

    public static string Key(string word) => SwiftText.TrimWhitespaceAndNewlines(word.ToLowerInvariant());

    public void Dispose() => _pack?.Dispose();
}
