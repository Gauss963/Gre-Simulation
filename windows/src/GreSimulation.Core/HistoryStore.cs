using System.Diagnostics;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GreSimulation.Core;

/// <summary>
/// Port of <c>HistoryStore</c> (Models.swift): saved score reports, newest first, in
/// <c>&lt;dataDirectory&gt;\ScoreHistory-v2.json</c> (same JSON as the Mac's
/// <c>Application Support/Gre Simulation/ScoreHistory-v2.json</c>). Results found under the legacy
/// key <c>gre-simulation.history.v1</c> in the <see cref="KeyValueStore"/> are merged in and the key is
/// removed once saved. Bind <see cref="Results"/> (PropertyChanged fires on every change).
/// </summary>
public sealed class HistoryStore : ObservableObject
{
    public const string FileName = "ScoreHistory-v2.json";
    public const string LegacyStorageKey = "gre-simulation.history.v1";

    private readonly KeyValueStore _store;
    private readonly string? _storagePath;
    private readonly object _gate = new();
    private IReadOnlyList<ExamResult> _results = [];

    /// <param name="dataDirectory">The App's data folder; null keeps history in the key-value store only (as the Mac does when Application Support is unavailable).</param>
    /// <param name="store">Holds the legacy history key.</param>
    public HistoryStore(string? dataDirectory, KeyValueStore store)
    {
        _store = store;
        _storagePath = MakeStoragePath(dataDirectory);
        var fileData = _storagePath is not null && File.Exists(_storagePath) ? TryRead(_storagePath) : null;
        var legacyData = store.GetData(LegacyStorageKey);
        var fileResults = fileData is not null ? DecodeHistory(fileData, "application support") : null;
        var legacyResults = legacyData is not null ? DecodeHistory(legacyData, "legacy preferences") : null;
        if (fileResults is null && legacyResults is null) return;
        var merged = new Dictionary<Guid, ExamResult>();
        foreach (var result in legacyResults ?? []) merged[result.Id] = result;
        foreach (var result in fileResults ?? []) merged[result.Id] = result;
        _results = merged.Values.OrderByDescending(r => r.CompletedAt).ToList();
        if (legacyResults is not null && Save()) store.Remove(LegacyStorageKey);
    }

    public IReadOnlyList<ExamResult> Results
    {
        get { lock (_gate) return _results; }
    }

    public string? StoragePath => _storagePath;

    /// <summary>Save &amp; Close on the score report: inserts the result at the top and saves.</summary>
    public void Add(ExamResult result)
    {
        lock (_gate) _results = new[] { result }.Concat(_results).ToList();
        Save();
        OnPropertyChanged(nameof(Results));
    }

    /// <summary>Clear history (after the "Delete every saved result?" confirmation).</summary>
    public void Clear()
    {
        lock (_gate) _results = [];
        Save();
        _store.Remove(LegacyStorageKey);
        OnPropertyChanged(nameof(Results));
    }

    private bool Save()
    {
        byte[] data;
        lock (_gate)
        {
            try { data = GreJson.SerializeToUtf8Bytes(_results); }
            catch (Exception error) when (error is JsonException or NotSupportedException) { return false; }
        }
        if (_storagePath is null)
        {
            _store.SetData(LegacyStorageKey, data);
            return true;
        }
        try
        {
            AtomicFile.WriteAllBytes(_storagePath, data);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Could not save score history: {error.Message}");
            return false;
        }
    }

    private static string? MakeStoragePath(string? dataDirectory)
    {
        if (dataDirectory is null) return null;
        try
        {
            Directory.CreateDirectory(dataDirectory);
            return Path.Combine(dataDirectory, FileName);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Could not prepare score-history storage: {error.Message}");
            return null;
        }
    }

    private static byte[]? TryRead(string path)
    {
        try { return File.ReadAllBytes(path); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return null; }
    }

    private static List<ExamResult>? DecodeHistory(byte[] data, string source)
    {
        try
        {
            return GreJson.Deserialize<List<ExamResult>>(data);
        }
        catch (JsonException error)
        {
            Debug.WriteLine($"Could not decode {source} score history: {error.Message}");
            return null;
        }
    }
}
