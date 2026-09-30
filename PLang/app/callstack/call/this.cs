using System.Diagnostics;
namespace app.callstack.call;

/// <summary>
/// One execution scope on the call tree. Pushed by App.Run before dispatching an action,
/// disposed via <c>await using</c> on scope exit which restores the AsyncLocal Current and
/// optionally removes self from <c>Caller.Children</c> when history is off.
///
/// Tree shape: navigate up via <see cref="Caller"/>, down via <see cref="Children"/>.
///
/// Render-agnostic: same data folds into a stack (Caller walk), flamegraph (Children walk),
/// or timeline (sort by StartedAt). A plang value: it writes itself as <see cref="Output"/> says.
/// </summary>
public sealed partial class @this : global::app.type.item.@this, IAsyncDisposable
{
    /// <summary>A structure — navigated by its members.</summary>
    public override bool IsLeaf => false;

    /// <summary>
    /// The frame writes itself short: its id and depth, where it is (the goal's address, the step's text, the
    /// action in its formal form — the whole program is one navigation away), when, whether its error was
    /// handled, its tags and errors, and its caller by id (a back-edge is written by name). The Debug view adds
    /// the variable changes it saw, the event it runs, and its children by id. Never its variables.
    /// </summary>
    public override async System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
    {
        writer.BeginObject();
        writer.Name("id"); writer.String(Id);
        writer.Name("depth"); writer.Long(Depth);
        if (Goal?.Address is { } address) { writer.Name("goal"); writer.String(address); }
        if (Step != null) { writer.Name("step"); writer.String(Step.Text); }
        if (Action != null)
        {
            writer.Name("action");
            if (context == null) writer.String(Action.ToString());
            else
            {
                var formal = new global::app.goal.step.action.formal.Writer();
                await Action.Output(formal, global::app.View.Store, context);
                writer.String(formal.ToString());
            }
        }
        if (StartedAt != default) { writer.Name("startedAt"); writer.DateTimeOffset(StartedAt); }
        if (Duration is { } duration) { writer.Name("duration"); writer.TimeSpan(duration); }
        writer.Name("handled"); writer.Bool(Handled);
        if (Tags.CountRaw > 0) { writer.Name("tags"); await Tags.Output(writer, mode, context); }
        if (Errors.Count > 0)
        {
            writer.Name("errors");
            writer.BeginArray(Errors.Count);
            foreach (var error in Errors) await error.Output(writer, mode, context);
            writer.EndArray();
        }
        if (Caller != null) { writer.Name("caller"); writer.String(Caller.Id); }
        if (mode == global::app.View.Debug)
        {
            if (Diffs is { Count: > 0 } diffs)
            {
                writer.Name("diffs");
                writer.BeginArray(diffs.Count);
                foreach (var diff in diffs)
                {
                    writer.BeginObject();
                    writer.Name("name"); writer.String(diff.Name);
                    writer.Name("at"); writer.DateTimeOffset(diff.At);
                    writer.EndObject();
                }
                writer.EndArray();
            }
            if (Event != null) { writer.Name("event"); await Event.Output(writer, mode, context); }
            if (Children.Count > 0)
            {
                writer.Name("children");
                writer.BeginArray(Children.Count);
                foreach (var child in Children) writer.String(child.Id);
                writer.EndArray();
            }
        }
        writer.EndObject();
    }

    private readonly Stopwatch? _stopwatch;
    private readonly app.callstack.@this _stack;
    private readonly @this? _previousCurrent;
    private readonly Variables? _diffSource;
    // A diff keeps a deep copy of the value before (the DeepDiff setting when pushed), else a summary.
    private readonly bool _deep;
    private Dictionary<global::System.Type, object>? _items;

    /// <summary>
    /// Unique identifier for this Call. 8 hex chars — short enough for log lines.
    /// </summary>
    public string Id { get; }

    /// <summary>The goal in play at this frame — the goal a goal's frame runs, or the one its step or action is in.
    /// Null for an action composed in C#, which holds no step.</summary>
    public global::app.goal.@this? Goal { get; }

    /// <summary>The step in play at this frame — the step a step's frame runs, or the action's. Null in a goal's
    /// own frame and for an action composed in C#.</summary>
    public global::app.goal.step.@this? Step { get; }

    /// <summary>The action this frame runs. Null in a goal's or a step's frame.</summary>
    public global::app.goal.step.action.@this? Action { get; }

    /// <summary>The event whose bound call is running in this frame, as <c>%!event%</c> reads it — its value the
    /// running event, its properties <c>item</c> and <c>result</c>. Set only while that call runs.</summary>
    public global::app.data.@this? Event { get; internal set; }

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
    /// Flipped <c>true</c> by on.error's Wrap on recovery success. Renderers use this to
    /// show "errored — recovered" vs "errored — uncaught."
    /// </summary>
    public bool Handled { get; set; }

    /// <summary>The error in play AT THIS FRAME — its newest observation, unless recovery has
    /// already succeeded here. <see cref="Handled"/> is what stops it: "recovered, stop being
    /// <c>%!error%</c>". Null when this frame never failed, or failed and was recovered.
    /// <see cref="app.callstack.@this.Error"/> walks <see cref="Caller"/> asking each frame this.</summary>
    public global::app.error.Error? Error => Handled ? null : Errors.Newest;

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

    /// <summary>Wall time this frame has run — while in flight (an after-binding runs inside the frame) and, once
    /// popped, its whole duration. Null when the Timing flag was off at Push.</summary>
    public TimeSpan? Duration => _stopwatch?.Elapsed;

    // --- Diff tier (null when Flags.Diff off) ---
    /// <summary>
    /// Variable mutations observed during this Call's lifetime. Null unless Flags.Diff was
    /// on at Push. See <see cref="diff.@this"/> for thread-safety + snapshot iteration.
    /// </summary>
    public diff.@this? Diffs { get; }

    // --- Tag tier ---
    /// <summary>
    /// Free-form tags written through <see cref="Tag"/> — a plang dict, read as any dict
    /// (<c>%!callStack.Current.Caller.Tags.foo%</c>). Always allocated, so no lazy-init race.
    /// </summary>
    public global::app.type.item.dict.@this Tags { get; } = new();

    // Parallel branches tag one caller's frame; the frame serializes the merge into its dict.
    private readonly object _tagging = new();

    /// <summary>
    /// Constructed by <see cref="app.callstack.@this.Push"/>. Holds back-references to the
    /// owning stack and previous AsyncLocal current so DisposeAsync can restore them and
    /// remove self from Children when history is off.
    /// </summary>
    internal @this(
        global::app.goal.@this? goal,
        global::app.goal.step.@this? step,
        global::app.goal.step.action.@this? action,
        @this? caller,
        app.callstack.@this stack,
        @this? previousCurrent,
        Variables? diffSource)
    {
        Id = Guid.NewGuid().ToString("N")[..8];
        Goal = goal;
        Step = step;
        Action = action;
        Caller = caller;
        Depth = (caller?.Depth ?? 0) + 1;
        _stack = stack;
        _previousCurrent = previousCurrent;
        _diffSource = diffSource;
        Children = new child.list.@this(stack);

        if (stack.Timing.Value)
        {
            StartedAt = DateTimeOffset.UtcNow;
            _stopwatch = Stopwatch.StartNew();
        }

        if (stack.Diff.Value && diffSource != null)
        {
            Diffs = new diff.@this();
            _deep = stack.DeepDiff.Value;
            stack.Open(this);
        }
    }

    /// <summary>
    /// A change of <paramref name="store"/>: when it is the store this frame captures, the diff lands here —
    /// <paramref name="name"/> held <paramref name="before"/>, null when it was new, so reverse-apply unwinds
    /// the create. Parallel branches sharing the store record concurrently; Diffs owns its lock.
    /// </summary>
    internal void Record(Variables store, string name, object? before)
    {
        if (ReferenceEquals(store, _diffSource))
            Diffs?.Add(new Diff(name, CaptureBefore(before, _deep), DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Merges <paramref name="tags"/> into this Call's <see cref="Tags"/> — each entry rides in as its typed
    /// binding, staying lazy; a key already there is overwritten. The <c>debug.tag</c> action's door.
    /// </summary>
    public void Tag(global::app.type.item.dict.@this tags, global::app.actor.context.@this context)
    {
        lock (_tagging)
            foreach (var entry in tags.Entries(context))
                Tags.Set(entry);
    }

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
    /// (only itself), <c>Root.Children[0].Depth == 2</c>, etc. A birth fact: one more than its
    /// caller's. PLang tests can <c>assert %!callStack.Current.Depth% equals 2</c>.
    /// </summary>
    public int Depth { get; }

    /// <summary>The frame records an error against itself — the one door. Stamps what the error does
    /// not carry yet: the failing chain, the context of the run it met here, and — when the error keeps
    /// them (<c>Keeps</c>: an assertion, or any error under --debug) — the variables as they are now. Then
    /// files it on the frame and in the run's audit.
    /// <para>Recording each error ONCE is the frame's own contract, kept by instance identity, so no
    /// caller guards: a retry mints a fresh error per attempt and each is kept (real history), while
    /// a layer that passes the same error through records nothing new.</para></summary>
    public void Record(global::app.error.Error error, actor.context.@this context)
    {
        if (error.CallFrames.Count == 0) error.CallFrames = SnapshotChain();
        error.Step ??= Step;
        error.Context ??= context;
        if (error.Variables == null && error.Keeps(context)) error.Variables = context.Variable.Snapshot();
        if (Errors.Any(x => ReferenceEquals(x, error))) return;
        Errors.Add(error);
        _stack.Audit.Add(error);
    }

    /// <summary>The action's index in its step; -1 when the frame runs no action, or its step doesn't hold it.</summary>
    public int Index => Action != null && Step != null ? Step.Code.IndexOf(Action) : -1;

    /// <summary>A resume point: an action its step holds. A goal's or a step's frame is not one, nor an action
    /// composed in C#, which no step holds — the resumed run makes those again.</summary>
    public bool IsResumable => Index >= 0;

    /// <summary>This frame's action where it stands, with this frame's Id — the snapshot surrogate; null in a
    /// goal's or a step's frame.</summary>
    public Position? Position => Action is { } action
        ? new Position(action, Goal!, Step?.Index ?? -1, Index, Id)
        : null;

    /// <summary>Its line in a stack trace: <c>Start.set (step 3) in /Start.goal</c>.</summary>
    public override string ToString()
    {
        var name = Goal?.Name ?? Action?.Module.Name ?? "?";
        if (Action != null) name += "." + Action.Name;
        var step = Step != null ? $" (step {Step.Index + 1})" : "";
        var path = Goal?.Path != null ? $" in {Goal.Path}" : "";
        return name + step + path;
    }

    /// <summary>
    /// Disposes the Call: stops the stopwatch, ends its diff capture, restores
    /// AsyncLocal Current, and (when history off) removes self from Caller.Children.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        if (_stopwatch != null)
        {
            _stopwatch.Stop();
            CompletedAt = DateTimeOffset.UtcNow;
        }

        if (Diffs != null) _stack.Close(this);

        if (!_stack.History.Value && Caller != null)
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
            // A value the cloner can't copy (no reflection access, an unsupported member) is
            // captured as its summary; any other failure is a bug and bubbles.
            try { return Force.DeepCloner.DeepClonerExtensions.DeepClone(value); }
            catch (System.Exception ex) when (ex is NotSupportedException or MemberAccessException
                or System.Security.SecurityException or InvalidOperationException)
            {
                return SummaryString(value);
            }
        }
        return SummaryString(value);
    }

    private static bool IsScalar(object value) =>
        value switch
        {
            global::app.type.item.text.@this t => t.Length.ToInt32() <= 256,
            // Scalar wrappers are deeply immutable (the wrapper-immutability
            // gate) — holding the instance as Before is safe, no clone needed.
            global::app.type.item.@this i => i.IsLeaf,
            string s => s.Length <= 256,
            bool or int or long or short or byte or sbyte or uint or ulong or ushort => true,
            float or double or decimal => true,
            DateTime or DateTimeOffset or TimeSpan or Guid => true,
            _ => false
        };

    private static string SummaryString(object value)
    {
        // A native plang collection summarises by its plang type name and item
        // count — never the C# class name ("this"), which leaks the internal type
        // into user-facing debug output.
        if (value is global::app.type.item.list.@this list)
            return $"<list @ {list.CountRaw} items>";
        if (value is global::app.type.item.dict.@this dict)
            return $"<dict @ {dict.CountRaw} items>";
        var t = value.GetType();
        if (value is System.Collections.ICollection col)
            return $"<{t.Name} @ {col.Count} items>";
        return $"<{t.Name}>";
    }
}
