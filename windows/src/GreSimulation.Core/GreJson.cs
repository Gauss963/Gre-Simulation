using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GreSimulation.Core;

/// <summary>
/// System.Text.Json settings that read and write the same JSON as Swift's default
/// <c>JSONEncoder</c>/<c>JSONDecoder</c>: camelCase keys, enum raw values, nil optionals omitted,
/// sets as arrays, <c>Date</c> as seconds since 2001-01-01 UTC, and <c>UUID</c> as an upper-case string.
/// </summary>
public static class GreJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            NumberHandling = JsonNumberHandling.Strict,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        options.Converters.Add(new SwiftDateConverter());
        options.Converters.Add(new SwiftUuidConverter());
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }

    public static T? Deserialize<T>(ReadOnlySpan<byte> utf8) => JsonSerializer.Deserialize<T>(utf8, Options);
    public static T? Deserialize<T>(Stream stream) => JsonSerializer.Deserialize<T>(stream, Options);
    public static byte[] SerializeToUtf8Bytes<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Options);

    /// <summary>Swift's reference date (Date(timeIntervalSinceReferenceDate: 0)).</summary>
    public static readonly DateTimeOffset ReferenceDate = new(2001, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static double ToSwiftTimeInterval(DateTimeOffset date) => (date - ReferenceDate).Ticks / (double)TimeSpan.TicksPerSecond;

    public static DateTimeOffset FromSwiftTimeInterval(double seconds) =>
        ReferenceDate.AddTicks((long)Math.Round(seconds * TimeSpan.TicksPerSecond));

    private sealed class SwiftDateConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            FromSwiftTimeInterval(reader.GetDouble());

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
            writer.WriteNumberValue(ToSwiftTimeInterval(value));
    }

    private sealed class SwiftUuidConverter : JsonConverter<Guid>
    {
        public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            Guid.Parse(reader.GetString() ?? throw new JsonException("UUID must be a string."), CultureInfo.InvariantCulture);

        public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString("D").ToUpperInvariant());
    }
}
