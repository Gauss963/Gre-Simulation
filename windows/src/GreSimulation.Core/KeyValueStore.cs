using System.Text.Json;
using System.Text.Json.Nodes;

namespace GreSimulation.Core;

/// <summary>
/// The Windows stand-in for <c>UserDefaults.standard</c> / <c>@AppStorage</c>: a JSON object saved in
/// <c>&lt;dataDirectory&gt;\Preferences.json</c>, using the same key names as the Mac app.
/// Thread-safe; every change is written atomically (temporary file, then replace).
/// Construct with <c>null</c> for an in-memory store (tests).
/// </summary>
/// <remarks>
/// Keys in use: <c>gre-simulation.question-exposure.v1.verbal</c>, <c>…quantitative</c>,
/// <c>gre-simulation.question-family-exposure.v1.quantitative</c>, <c>gre-simulation.flashcards.mastered.v1</c>,
/// <c>gre-simulation.flashcards.mode</c>, and the legacy history key <c>gre-simulation.history.v1</c>.
/// </remarks>
public sealed class KeyValueStore
{
    public const string FileName = "Preferences.json";

    private readonly object _gate = new();
    private readonly string? _path;
    private readonly JsonObject _values;

    public KeyValueStore(string? dataDirectory)
    {
        if (dataDirectory is not null)
        {
            Directory.CreateDirectory(dataDirectory);
            _path = Path.Combine(dataDirectory, FileName);
        }
        _values = Load(_path);
    }

    /// <summary>The backing file, or null for an in-memory store.</summary>
    public string? FilePath => _path;

    private static JsonObject Load(string? path)
    {
        if (path is null || !File.Exists(path)) return new JsonObject();
        try
        {
            return JsonNode.Parse(File.ReadAllBytes(path)) as JsonObject ?? new JsonObject();
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            // A damaged preferences file must not stop the app; start over as UserDefaults would.
            return new JsonObject();
        }
    }

    public bool Contains(string key)
    {
        lock (_gate) return _values.ContainsKey(key);
    }

    public string? GetString(string key)
    {
        lock (_gate)
        {
            return _values[key] is JsonValue value && value.TryGetValue(out string? text) ? text : null;
        }
    }

    public void SetString(string key, string? value) => Set(key, value is null ? null : JsonValue.Create(value));

    /// <summary>Like <c>UserDefaults.stringArray(forKey:)</c>: null when missing or not an array of strings.</summary>
    public IReadOnlyList<string>? GetStringArray(string key)
    {
        lock (_gate)
        {
            if (_values[key] is not JsonArray array) return null;
            var result = new List<string>(array.Count);
            foreach (var item in array)
            {
                if (item is JsonValue value && value.TryGetValue(out string? text)) result.Add(text);
                else return null;
            }
            return result;
        }
    }

    public void SetStringArray(string key, IEnumerable<string> values) =>
        Set(key, new JsonArray(values.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray()));

    public bool GetBool(string key, bool fallback = false)
    {
        lock (_gate)
        {
            return _values[key] is JsonValue value && value.TryGetValue(out bool flag) ? flag : fallback;
        }
    }

    public void SetBool(string key, bool value) => Set(key, JsonValue.Create(value));

    /// <summary>Binary data (<c>UserDefaults.data(forKey:)</c>), stored as a base64 string.</summary>
    public byte[]? GetData(string key)
    {
        var text = GetString(key);
        if (text is null) return null;
        try { return Convert.FromBase64String(text); }
        catch (FormatException) { return null; }
    }

    public void SetData(string key, byte[] data) => SetString(key, Convert.ToBase64String(data));

    public void Remove(string key) => Set(key, null);

    private void Set(string key, JsonNode? value)
    {
        lock (_gate)
        {
            if (value is null)
            {
                if (!_values.Remove(key)) return;
            }
            else
            {
                _values[key] = value;
            }
            Save();
        }
    }

    private void Save()
    {
        if (_path is null) return;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(_values, new JsonSerializerOptions { WriteIndented = true });
        AtomicFile.WriteAllBytes(_path, bytes);
    }
}

/// <summary>Atomic replace (Swift's <c>Data.write(to:options:.atomic)</c>).</summary>
public static class AtomicFile
{
    public static void WriteAllBytes(string path, byte[] bytes)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
