namespace app.error.list;

/// <summary>
/// Errors namespace root: AsyncLocal-flowed current error scope + run-wide audit.
/// Replaces the old <c>Actor.Context.Error</c> property and the parallel
/// <c>vars.Set("!error", ...)</c> registration.
///
/// <c>%!error%</c> in PLang resolves through <see cref="Error"/>: an error.handle.Wrap
/// pushes the caught error before invoking its recovery body and disposes the scope after,
/// so nested handlers see their own error and parallel branches don't pollute each other
/// (AsyncLocal forks naturally on Task.WhenAll).
/// </summary>
public sealed partial class @this
{
    // Instance-level (not static) AsyncLocal — multiple App instances in the same test
    // process must not see each other's Current. Mirrors CallStack/this.cs which moved
    // _current to instance scope for the same reason; before this fix, `All` was already
    // per-instance but `Error` read process-wide static.
    private readonly AsyncLocal<IError?> _current = new();

    /// <summary>
    /// Owning App, taken at construction. Used by <see cref="Push"/> to auto-flip
    /// <c>CallStack.Diff</c> on for the duration of an error scope so
    /// <see cref="Variables.@this.SnapshotAt"/> can reverse-apply post-throw mutations.
    /// Off everywhere else — pay-per-error cost.
    /// </summary>
    private readonly app.@this _app;

    public @this(app.@this app) { _app = app; }

    /// <summary>
    /// The current error in this async context. Null outside any <see cref="Push"/> scope.
    /// PLang reads this through <c>%!error%</c>.
    /// </summary>
    public IError? Error => _current.Value;

    /// <summary>Count of errors in the trail.</summary>
    public int Count => Trail.Count;

    /// <summary>Enumerate the trail.</summary>
    public IEnumerable<IError> list => Trail;

    /// <summary>
    /// Run-wide trail of every error pushed into scope. Survives Pop.
    /// See <see cref="trail.@this"/> for thread-safety + lifecycle.
    /// </summary>
    public trail.@this Trail { get; private set; } = new();

    /// <summary>
    /// Replaces the current Trail with one populated from a captured snapshot
    /// and freezes it. Called by trail.@this.Restore through App.Restore.
    /// </summary>
    internal void RestoreTrail(IEnumerable<IError> entries)
    {
        var rebuilt = new trail.@this();
        rebuilt.LoadAndFreeze(entries);
        Trail = rebuilt;
    }

    /// <summary>
    /// Pushes <paramref name="error"/> as the current error and appends to <see cref="Trail"/>.
    /// The returned disposable restores the previous AsyncLocal value on Dispose — use
    /// <c>using</c> or <c>using var</c> at the call site.
    /// </summary>
    public IDisposable Push(IError error, app.actor.context.@this context)
    {
        var previous = _current.Value;
        _current.Value = error;
        Trail.Add(error);
        // Wire App back-reference so error.Callback can materialise via app.Snapshot().
        if (error is Error e && e.App == null) e.App = _app;

        // Auto-flip CallStack.Diff on for the duration of error processing so handler-time
        // mutations land on the diff stream — Variables.SnapshotAt(error) reverse-applies
        // them to project back to throw-time state. Also wire a CallStack-level OnSet
        // subscription so the stream actually populates regardless of when live Calls
        // were pushed (per-Call subscription is decided at Push time and can't backfill).
        // The scope belongs to the actor whose flow threw — reach its stack via the
        // context, never a global CurrentActor pointer.
        var stack = context.CallStack;
        var priorDiff = stack.Diff;
        stack.Diff = global::app.type.item.@bool.@this.True;
        stack.EnableDiffStream(context.Variable);

        return new Restorer(_current, previous, stack, priorDiff);
    }

    private sealed class Restorer : IDisposable
    {
        private readonly AsyncLocal<IError?> _slot;
        private readonly IError? _previous;
        private readonly app.callstack.@this? _stack;
        private readonly global::app.type.item.@bool.@this? _priorDiff;
        private bool _disposed;

        public Restorer(AsyncLocal<IError?> slot, IError? previous,
                        app.callstack.@this? stack, global::app.type.item.@bool.@this? priorDiff)
        {
            _slot = slot;
            _previous = previous;
            _stack = stack;
            _priorDiff = priorDiff;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _slot.Value = _previous;
            if (_stack != null)
            {
                _stack.DisableDiffStream();
                if (_priorDiff != null) _stack.Diff = _priorDiff;
            }
        }
    }
}
