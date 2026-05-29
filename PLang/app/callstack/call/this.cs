using System.Diagnostics;
using app.error;
using ActionEntity = app.goal.steps.step.actions.action.@this;

namespace app.callstack.call;

/// <summary>
/// One execution scope on the call tree. Pushed by App.Run before dispatching an action,
/// disposed via <c>await using</c> on scope exit which restores the AsyncLocal Current and
/// optionally removes self from <c>Caller.Children</c> when history is off.
///
/// Tree shape: navigate up via <see cref="Caller"/>, down via <see cref="Children"/>.
///
/// Render-agnostic: same data folds into a stack (Caller walk), flamegraph (Children walk),
/// or timeline (sort by StartedAt).
/// </summary>
public sealed partial class @this : IAsyncDisposable
{
    private readonly Stopwatch? _stopwatch;
    private readonly app.callstack.@this _stack;
    private readonly @this? _previousCurrent;
    private readonly Variables? _diffSource;
    private Action<string, object?, object?>? _onSetHandler;
    private Action<string, object?>? _onCreateHandler;
    private Dictionary<global::System.Type, object>? _items;

    /// <summary>
    /// Unique identifier for this Call. 8 hex chars — short enough for log lines.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// The action being executed. OBP ref — navigate <c>Action.Step.Goal.Parent</c> for
    /// the static call site.
    /// </summary>
    public ActionEntity Action { get; }

    /// <summary>
    /// Sync parent in this execution chain — whatever AsyncLocal.Current was at Push time.
    /// Walk this for the "stack trace" view.
    /// </summary>
    public @this? Caller { get; }

    /// <summary>
    /// Errors observed at this scope. Populated by App.Run when the handler returns a
    /// failure or throws. <see cref="Handled"/> tracks recovery outcome independently —
    /// the error stays in the list either way (audit trail). See <see cref="error.@this"/>
    /// for thread-safety semantics.
    /// </summary>
    public error.@this Errors { get; } = new();

    /// <summary>
    /// Flipped <c>true</c> by error.handle.Wrap on recovery success. Renderers use this to
    /// show "errored — recovered" vs "errored — uncaught."
    /// </summary>
    public bool Handled { get; set; }

    /// <summary>
    /// Mirror of <see cref="Action.@this.Synthetic"/> stamped at Push time. False
    /// for PR-built actions (the wire-restorable case); true for C#-composed
    /// actions. Snapshot wire-serialisation filters synthetic frames out since
    /// they're recreated naturally by the resumed execution.
    /// </summary>
    public bool Synthetic { get; }

    /// <summary>
    /// Live siblings under this Call. Owns its own lock + FIFO eviction policy — see
    /// <see cref="child.list.@this"/>. Allocated lazily via the constructor below so the
    /// back-reference to the parent CallStack is set before any Add can land.
    /// </summary>
    public child.list.@this Children { get; }

    // --- Timing tier (default(DateTimeOffset) when Flags.Timing off) ---
    /// <summary>UTC timestamp at Push. <c>default(DateTimeOffset)</c> when Timing flag off.</summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>UTC timestamp at Pop. Null while in flight.</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Wall duration. Null until Pop.</summary>
    public TimeSpan? Duration => CompletedAt - StartedAt;

    // --- Diff tier (null when Flags.Diff off) ---
    /// <summary>
    /// Variable mutations observed during this Call's lifetime. Null unless Flags.Diff was
    /// on at Push. See <see cref="diff.@this"/> for thread-safety + snapshot iteration.
    /// </summary>
    public diff.@this? Diffs { get; }

    // --- Tag tier ---
    /// <summary>
    /// Free-form tags written by handlers (<c>tag</c> action) or C# code via <see cref="Tag"/>.
    /// Always allocated (cost is one dict alloc per Call) so the lazy-init race goes away —
    /// see <see cref="tag.@this"/> for thread-safety + iteration semantics.
    /// </summary>
    public tag.@this Tags { get; } = new();

    /// <summary>
    /// Constructed by <see cref="app.callstack.@this.Push"/>. Holds back-references to the
    /// owning stack and previous AsyncLocal current so DisposeAsync can restore them and
    /// remove self from Children when history is off.
    /// </summary>
    internal @this(
        ActionEntity action,
        @this? caller,
        app.callstack.@this stack,
        Flags flags,
        @this? previousCurrent,
        Variables? diffSource)
    {
        Id = Guid.NewGuid().ToString("N")[..8];
        Action = action;
        Caller = caller;
        Synthetic = action.Synthetic;
        _stack = stack;
        _previousCurrent = previousCurrent;
        _diffSource = diffSource;
        Children = new child.list.@this(stack);

        if (flags.Timing)
        {
            StartedAt = DateTimeOffset.UtcNow;
            _stopwatch = Stopwatch.StartNew();
        }

        if (flags.Diff && diffSource != null)
        {
            Diffs = new diff.@this();
            var deep = flags.DeepDiff;
            _onSetHandler = (name, before, _) =>
            {
                // OnSet fires synchronously on Variables.Set; parallel Task.WhenAll
                // branches sharing the same Variables instance can invoke this concurrently.
                // Diffs owns its lock and snapshot iteration — Add is safe, readers safe.
                Diffs.Add(new Diff(name, CaptureBefore(before, deep), DateTimeOffset.UtcNow));
            };
            // OnCreate fires for first-time variable creation (replacing OnSet for that path).
            // Capture as a diff with Before=null so reverse-apply unwinds the create.
            _onCreateHandler = (name, _) =>
            {
                Diffs.Add(new Diff(name, null, DateTimeOffset.UtcNow));
            };
            diffSource.OnSet += _onSetHandler;
            diffSource.OnCreate += _onCreateHandler;
        }
    }

    /// <summary>
    /// Writes a single tag onto this Call. Used by C# handlers (<c>cache.hit=true</c>,
    /// <c>http.status=503</c>, <c>llm.tokens=2400</c>) and by the <c>tag</c> PLang action.
    /// Thread-safe — Tags owns its lock.
    /// </summary>
    public void Tag(string key, string value) => Tags.Set(key, value);

    /// <summary>
    /// Typed metadata bag. Use this to attach handler-specific structured data
    /// (cache info, http status, llm token counts, schedule identity, callback identity).
    /// Returns null when nothing of <typeparamref name="T"/> has been set.
    /// </summary>
    public T? GetItem<T>() where T : class
    {
        if (_items == null) return null;
        return _items.TryGetValue(typeof(T), out var value) ? value as T : null;
    }

    /// <summary>
    /// Stores a typed metadata value on this Call. Lazy-allocates the bag on first call.
    /// Last-write-wins per type.
    /// </summary>
    public void SetItem<T>(T value) where T : class
    {
        _items ??= new Dictionary<global::System.Type, object>();
        _items[typeof(T)] = value!;
    }

    /// <summary>
    /// Returns <c>[this, Caller, Caller.Caller, ..., Root]</c>. Stable refs only — no copy.
    /// Used by App.Run to attach a chain to ServiceError on exception. Index <c>[0]</c> is
    /// always the failing Call (behavior tweak vs the old shape, which excluded self).
    /// </summary>
    public IReadOnlyList<@this> SnapshotChain()
    {
        var chain = new List<@this>();
        var current = this;
        while (current != null)
        {
            chain.Add(current);
            current = current.Caller;
        }
        return chain;
    }

    /// <summary>
    /// PLang-friendly view of the Caller chain — same data as <see cref="SnapshotChain"/>
    /// but exposed as a property so PLang dot-path resolution can reach it without method
    /// invocation, and so <c>- foreach %!callStack.Current.Chain%, call ...</c> iterates
    /// from PLang. Computed each access (cheap — Caller chain depth is typically small).
    /// </summary>
    public IReadOnlyList<@this> Chain => SnapshotChain();

    /// <summary>
    /// Length of the synchronous Caller chain rooted at this Call. <c>Root.Depth == 1</c>
    /// (only itself), <c>Root.Children[0].Depth == 2</c>, etc. Derived — walks Caller.
    /// PLang tests can <c>assert %!callStack.Current.Depth% equals 2</c>.
    /// </summary>
    public int Depth
    {
        get
        {
            int count = 0;
            var node = this;
            while (node != null) { count++; node = node.Caller; }
            return count;
        }
    }

    /// <summary>
    /// Executes the resolved handler under this Call frame. Wraps:
    ///   - <c>handler.ExecuteAsync(action, context)</c> invocation
    ///   - error stamping: <c>SnapshotParams</c> onto <c>Error.Params</c>,
    ///     <c>CallFrames</c> from <see cref="SnapshotChain"/> if not already set
    ///   - this.Errors.Add and CallStack.Audit.Add on failure
    ///   - OperationCanceledException swallowing into ServiceError (timeout.after
    ///     contract: inner action's generated ExecuteAsync swallows OCE; this
    ///     catch is the safety net for handlers that bubble it differently)
    /// Returns the handler's result (or a ServiceError-wrapped result on
    /// caught exception).
    /// </summary>
    public async Task<data.@this> ExecuteAsync(module.ICodeGenerated handler, actor.context.@this context)
    {
        try
        {
            var result = await handler.ExecuteAsync(Action, context);
            // Stamp __SnapshotParams onto Error.Params if the handler returned an error
            // without one already populated. (Generator no longer attaches snapshots
            // inside ExecuteAsync — that responsibility lives here.)
            if (!result.Success && result.Error is Error err)
            {
                if (err.Params == null) err.Params = handler.SnapshotParams();
                // Capture the failing Call chain so error.handle (and other downstream
                // observers) can identify the failing Call after this scope's Push pops.
                // Snapshot is index-[0]=self, walking Caller upward — matches the
                // ServiceError catch path below.
                if (err.CallFrames.Count == 0) err.CallFrames = SnapshotChain();
                Errors.Add(result.Error!);
                _stack.Audit.Add(result.Error!);
            }
            return result;
        }
        // Deliberately catches OperationCanceledException — timeout.after depends on this:
        // the inner action's generated ExecuteAsync swallows OCE into a ServiceError result,
        // so timeout.after detects the timeout via CTS state + failed result, not via OCE
        // bubbling up. Step.RunAsync's catch DOES exclude OCE — that asymmetry is intentional.
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            var serviceErr = new ServiceError(
                ex.Message, Action.Step!, SnapshotChain(), "ServiceError", 400) { Exception = ex };
            serviceErr.Params = handler.SnapshotParams();
            Errors.Add(serviceErr);
            _stack.Audit.Add(serviceErr);
            return data.@this.FromError(serviceErr);
        }
    }

    /// <summary>
    /// Disposes the Call: stops the stopwatch, unsubscribes diff capture, restores
    /// AsyncLocal Current, and (when history off) removes self from Caller.Children.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        if (_stopwatch != null)
        {
            _stopwatch.Stop();
            CompletedAt = DateTimeOffset.UtcNow;
        }

        if (_onSetHandler != null && _diffSource != null)
        {
            _diffSource.OnSet -= _onSetHandler;
            _onSetHandler = null;
        }
        if (_onCreateHandler != null && _diffSource != null)
        {
            _diffSource.OnCreate -= _onCreateHandler;
            _onCreateHandler = null;
        }

        if (!_stack.Flags.History && Caller != null)
            Caller.Children.Remove(this);

        // AsyncLocal restore: only flip back if we're still the Current. If a parallel branch
        // has its own Current, we leave that alone.
        _stack.RestoreCurrent(this, _previousCurrent);

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Capture rule for diff Before values. Scalars (int/bool/decimal/DateTimeOffset/short
    /// strings) pass through; non-scalars become summary strings unless DeepDiff is on,
    /// in which case they're deep-cloned. Default-scalar capture mitigates the OOM scenario
    /// observed under tight loops with large lists.
    /// </summary>
    private static object? CaptureBefore(object? value, bool deep)
    {
        if (value == null) return null;
        if (IsScalar(value)) return value;
        if (deep)
        {
            try { return Force.DeepCloner.DeepClonerExtensions.DeepClone(value); }
            catch (System.Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
            {
                return SummaryString(value);
            }
        }
        return SummaryString(value);
    }

    private static bool IsScalar(object value) =>
        value switch
        {
            string s => s.Length <= 256,
            bool or int or long or short or byte or sbyte or uint or ulong or ushort => true,
            float or double or decimal => true,
            DateTime or DateTimeOffset or TimeSpan or Guid => true,
            _ => false
        };

    private static string SummaryString(object value)
    {
        var t = value.GetType();
        if (value is System.Collections.ICollection col)
            return $"<{t.Name} @ {col.Count} items>";
        return $"<{t.Name}>";
    }
}
