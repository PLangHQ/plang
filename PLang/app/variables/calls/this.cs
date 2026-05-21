namespace app.variables.calls;

/// <summary>
/// Per-call parameter scopes for <see cref="app.variables.@this"/>.
///
/// Goal-call boundaries (and any other code that wants per-call name binding) push a
/// <see cref="call.@this"/> frame here. <see cref="app.variables.@this.Get"/> consults
/// <see cref="Current"/> first, then falls back to the actor-shared dictionary.
///
/// AsyncLocal scoping means concurrent calls on the same actor each see their own
/// frame — solves the race where two concurrent goal-channel writes both wanted
/// <c>%!data%</c> to mean different things on the same shared <see cref="Variables"/>.
///
/// Mirrors the shape of <see cref="app.callstack.@this"/> (Push returns
/// <see cref="IAsyncDisposable"/>, RestoreCurrent no-ops if Current already moved on)
/// — kept structurally separate because CallStack is action-grained observability and
/// this is goal-grained name resolution.
/// </summary>
public sealed class @this
{
    private readonly AsyncLocal<call.@this?> _current = new();

    /// <summary>The innermost Call in this async flow. Null when no Push has happened on this branch.</summary>
    public call.@this? Current => _current.Value;

    /// <summary>
    /// Pushes a new <see cref="call.@this"/> bound to <paramref name="parameters"/>.
    /// Returns the Call — <c>await using</c> for automatic Pop.
    /// Null or empty parameter sequence still pushes a frame (so an outer Get falls through cleanly);
    /// passing null for parameters yields an empty frame.
    /// </summary>
    public call.@this Push(IEnumerable<data.@this>? parameters)
    {
        var caller = _current.Value;
        var call = new call.@this(parameters, caller, this);
        _current.Value = call;
        return call;
    }

    /// <summary>
    /// Restores Current to <paramref name="previous"/> if <paramref name="leaving"/> is still
    /// the active value. Called by <see cref="call.@this.DisposeAsync"/>. No-op if Current
    /// already moved on (out-of-order disposal, e.g. unhandled exception unwinding past nested
    /// frames) — matches CallStack's restore semantic.
    /// </summary>
    internal void RestoreCurrent(call.@this leaving, call.@this? previous)
    {
        if (ReferenceEquals(_current.Value, leaving))
            _current.Value = previous;
    }
}
