namespace app.callstack;

/// <summary>
/// Per-property gates for richer Call data capture. Default is all-false (structural only)
/// — the thin Action/Caller/Errors push/pop costs ~50ns and is always on.
/// Each flag toggles population of one tier of data on Call.@this:
///  - <see cref="Timing"/>: StartedAt/CompletedAt/Duration
///  - <see cref="Diff"/>: Variables.OnSet → Call.Diffs (scalar-only by default)
///  - <see cref="DeepDiff"/>: deep-clone non-scalar Before values (only meaningful with Diff)
///  - <see cref="Tags"/>: advisory hint for exporters/serializers. Writes via Call.Tag()
///    always succeed — explicit observability intent (the user wrote <c>- tag x=y</c> or a
///    C# handler emitted a diagnostic) isn't gated. The flag is reserved for downstream
///    consumers that want to suppress tag rendering.
///  - <see cref="History"/>: retain popped Calls in Caller.Children (FIFO-capped at MaxFrames)
///  - <see cref="MaxFrames"/>: history-on retention cap
/// Parsed from <c>--debug={callstack:{...}}</c> via <see cref="Parse"/>.
/// </summary>
public record struct Flags(
    bool Timing,
    bool Diff,
    bool DeepDiff,
    bool Tags,
    bool History,
    int MaxFrames)
{
    /// <summary>Default flag set — all off, MaxFrames 1000.</summary>
    public static Flags Default => new(false, false, false, false, false, 1000);

    /// <summary>Shorthand for <c>--debug={callstack:true}</c> — Timing + Tags on, others off.</summary>
    public static Flags Shorthand => new(Timing: true, Diff: false, DeepDiff: false, Tags: true, History: false, MaxFrames: 1000);

    /// <summary>
    /// Parses <c>--debug={callstack:...}</c> into a Flags value.
    ///   <c>callstack:true</c> → <see cref="Shorthand"/> (Timing + Tags on, others off).
    ///   <c>callstack:{timing:true,...}</c> → field-by-field from the dict.
    /// Anything else (false, null, malformed) → <see cref="Default"/>.
    /// Defensive parser — bad input doesn't throw, it falls back to all-off.
    /// </summary>
    public static Flags Parse(object? raw)
    {
        if (raw is bool b)
            return b ? Shorthand : Default;

        if (raw is IDictionary<string, object?> obj)
        {
            return new Flags(
                Timing:    GetBool(obj, "timing"),
                Diff:      GetBool(obj, "diff"),
                DeepDiff:  GetBool(obj, "deepDiff") || GetBool(obj, "deepdiff"),
                Tags:      GetBool(obj, "tags"),
                History:   GetBool(obj, "history"),
                MaxFrames: GetInt(obj, "maxFrames", 1000));
        }

        return Default;
    }

    private static bool GetBool(IDictionary<string, object?> obj, string key)
    {
        if (!obj.TryGetValue(key, out var raw) || raw == null) return false;
        return raw switch
        {
            bool b => b,
            string s when bool.TryParse(s, out var parsed) => parsed,
            _ => false
        };
    }

    private static int GetInt(IDictionary<string, object?> obj, string key, int fallback)
    {
        if (!obj.TryGetValue(key, out var raw) || raw == null) return fallback;
        return raw switch
        {
            int i => i,
            long l => (int)l,
            double d => (int)d,
            string s when int.TryParse(s, out var parsed) => parsed,
            _ => fallback
        };
    }
}
