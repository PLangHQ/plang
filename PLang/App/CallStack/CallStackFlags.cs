namespace App.CallStack;

/// <summary>
/// Per-property gates for richer Call data capture. Default is all-false (structural only)
/// — the thin Action/Caller/Cause/Errors push/pop costs ~50ns and is always on.
/// Each flag toggles population of one tier of data on Call.@this:
///  - <see cref="Timing"/>: StartedAt/CompletedAt/Duration
///  - <see cref="Diff"/>: Variables.OnSet → Call.Diffs (scalar-only by default)
///  - <see cref="DeepDiff"/>: deep-clone non-scalar Before values (only meaningful with Diff)
///  - <see cref="Tags"/>: advisory hint for exporters/serializers. Writes via Call.Tag()
///    always succeed and lazy-allocate the dict — explicit observability intent (the user
///    wrote `- tag x=y` or a C# handler emitted a diagnostic) shouldn't be gated. The flag
///    is reserved for downstream consumers that want to suppress tag rendering.
///  - <see cref="History"/>: retain popped Calls in Caller.Children (FIFO-capped at MaxFrames)
///  - <see cref="MaxFrames"/>: history-on retention cap
/// Parsed from <c>--debug={callstack:{...}}</c>.
/// </summary>
public record struct CallStackFlags(
    bool Timing,
    bool Diff,
    bool DeepDiff,
    bool Tags,
    bool History,
    int MaxFrames)
{
    /// <summary>Default flag set — all off, MaxFrames 1000.</summary>
    public static CallStackFlags Default => new(false, false, false, false, false, 1000);

    /// <summary>Shorthand for <c>--debug={callstack:true}</c> — Timing + Tags on, others off.</summary>
    public static CallStackFlags Shorthand => new(Timing: true, Diff: false, DeepDiff: false, Tags: true, History: false, MaxFrames: 1000);
}
