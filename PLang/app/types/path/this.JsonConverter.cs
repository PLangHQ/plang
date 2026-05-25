using System.Text.Json;
using System.Text.Json.Serialization;

namespace app.types.path;

/// <summary>
/// AsyncLocal scope carrying the actor context to <see cref="JsonConverter"/> during
/// Path deserialization. Pushed by callers that have a Context in scope
/// (FilePath.ReadText for .pr, App.Load, …); read by <see cref="JsonConverter"/> to
/// call <see cref="@this.Resolve(string, actor.context.@this)"/> on the wire string
/// so the resulting Path is fully Context-wired the moment it lands.
///
/// Without a pushed scope the converter falls back to a stub Path with Raw set and
/// Context = null — the existing back-reference passes still work, but accessing
/// Context-dependent accessors (Relative, MimeType) will throw until something
/// wires Context.
/// </summary>
public static class DeserializationScope
{
    private static readonly System.Threading.AsyncLocal<actor.context.@this?> _current = new();

    public static actor.context.@this? Current => _current.Value;

    public readonly struct Disposable : System.IDisposable
    {
        private readonly actor.context.@this? _previous;
        internal Disposable(actor.context.@this? previous) { _previous = previous; }
        public void Dispose() { _current.Value = _previous; }
    }

    public static Disposable Push(actor.context.@this context)
    {
        var prev = _current.Value;
        _current.Value = context;
        return new Disposable(prev);
    }
}

/// <summary>
/// STJ converter for <see cref="@this"/>. Wire shape: the path's <c>Relative</c>
/// string (portable across roots). Read side dispatches through
/// <see cref="@this.Resolve(string, actor.context.@this)"/> when
/// <see cref="DeserializationScope.Current"/> is set, so the resulting Path is in
/// the right scheme and fully Context-wired. Without scope the converter returns
/// a file Path with Raw set and Context null (the existing GoalCall.LoadFromFile
/// back-reference pass fills it in later).
/// </summary>
public sealed class JsonConverter : JsonConverter<@this>
{
    public override @this? Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Expected string for path, got {reader.TokenType}");

        var raw = reader.GetString();
        if (string.IsNullOrEmpty(raw)) return null;

        var ctx = DeserializationScope.Current;
        if (ctx != null) return @this.Resolve(raw!, ctx);

        // No scope — build a file-scheme stub with Raw only. Context will be
        // wired post-deserialize by the caller's back-reference pass.
        return new file.@this(raw!, context: null) { Raw = raw! };
    }

    public override void Write(Utf8JsonWriter writer, @this value, JsonSerializerOptions options)
    {
        // Serialize as the portable Relative form. Falls back to Raw or Absolute
        // when Context isn't wired (no root anchor).
        string? wire = null;
        if (value.Context != null)
        {
            try { wire = value.Relative; } catch { wire = null; }
        }
        wire ??= !string.IsNullOrEmpty(value.Raw) ? value.Raw : value.Absolute;
        writer.WriteStringValue(wire);
    }
}
