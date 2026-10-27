using System.Diagnostics;
namespace app.call;

/// <summary>
/// One frame of a context's calls, holding its own variables: the names it was born with (a goal call's parameters)
/// and what is written to them while it runs. Pushed by its <see cref="global::app.call.list.@this"/>, disposed via
/// <c>await using</c>, which restores the current frame and, with history off, removes it from its caller's
/// <see cref="Children"/>. Each kind is its own type: a goal's (<see cref="goal.@this"/>), a step's
/// (<see cref="step.@this"/>), an action's (<see cref="action.@this"/>), one that only binds names (a loop's item, a
/// handler's error, <see cref="binding.@this"/>) and one that keeps every write (<see cref="isolated.@this"/>).
///
/// Tree shape: navigate up via <see cref="Caller"/>, down via <see cref="Children"/>. Reads walk the frames outward,
/// each frame's own variables first. A plang value: it writes itself as <see cref="Output"/> says.
/// </summary>
public partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>, IAsyncDisposable,
    global::app.type.item.property.IOwner
{
    /// <summary>A structure — navigated by its members.</summary>
    public override bool IsLeaf => false;

    /// <summary>A frame is a live place in a run, as a channel is: a copy of it is it.</summary>
    protected internal override global::app.type.item.@this Clone() => this;

    /// <summary>What a program adds to the call (<c>%!call.scope.retries%</c>) — kept as long as the call lives.</summary>
    // The frame is written by its tags: in a trace (Out, Debug) its id, depth, when, whether its error was handled,
    // its errors and the changes it saw; kept (Store) its element (the goal, step or action it runs, which a snapshot
    // writes as what finds it), its variables, what a program added, its tags, the event running in it and the error
    // in play. Its caller and children are where it sits, never written.
    [Store, global::app.LlmBuilder]
    public global::app.type.item.property.list.@this property { get; private set; }

    private readonly Stopwatch? _stopwatch;
    private readonly global::app.call.list.@this _stack;
    private readonly @this? _previousCurrent;
    private readonly Variables? _diffSource;
    // A diff keeps a deep copy of the value before (the diff.deep setting when pushed), else a summary.
    private readonly bool _deep;
    private Dictionary<global::System.Type, object>? _items;

    // The frame's facts as it holds them — a frame is pushed per action, so its plang faces below are made
    // when read, never at push.
    private readonly string _id;
    internal readonly int _depth;
    private bool _handled;
    private bool _ended;
    private readonly DateTimeOffset? _startedAt;
    private DateTimeOffset? _completedAt;

    /// <summary>
    /// Unique identifier for this Call. 8 hex chars — short enough for log lines.
    /// </summary>
    [Out, Debug]
    public global::app.type.item.text.@this Id => _id;

    /// <summary>The goal in play at this frame — the goal a goal's frame runs, or the one its step is in. Null for an
    /// action composed in C#, which holds no step.</summary>
    [global::app.LlmBuilder]
    public virtual global::app.goal.@this? Goal => Step?.Goal;

    /// <summary>The step in play at this frame — the step a step's frame runs, or the one its action is in. Null in a
    /// goal's own frame and for an action composed in C#.</summary>
    [global::app.LlmBuilder]
    public virtual global::app.goal.step.@this? Step => Action?.Step;

    /// <summary>The action this frame runs. Null in a goal's, a step's or a binding's frame.</summary>
    [global::app.LlmBuilder]
    public virtual global::app.goal.step.action.@this? Action => null;

    /// <summary>The event whose bound call is running in this frame, as <c>%!event%</c> reads it — its value the
    /// running event, its properties <c>item</c> and <c>result</c>. Set only while that call runs.</summary>
    [Store, Debug]
    public global::app.data.@this? Event { get; internal set; }

    /// <summary>
    /// Sync parent in this execution chain — whatever AsyncLocal.Current was at Push time.
    /// Walk this for the "stack trace" view.
    /// </summary>
    [global::app.LlmBuilder]
    public @this? Caller { get; }

    /// <summary>
    /// Errors observed at this scope. Populated by App.Run when the handler returns a
    /// failure or throws. <see cref="Handled"/> tracks recovery outcome independently —
    /// the error stays in the list either way (audit trail). See <see cref="error.@this"/>
    /// for thread-safety semantics.
    /// </summary>
    [Out, Debug, global::app.LlmBuilder]
    public global::app.error.list.@this Errors { get; }

    /// <summary>
    /// Flipped <c>true</c> by on.error's Wrap on recovery success. Renderers use this to
    /// show "errored — recovered" vs "errored — uncaught."
    /// </summary>
    [Out, Debug]
    public global::app.type.item.@bool.@this Handled { get => _handled; set => _handled = value.Value; }

    /// <summary>The error in play AT THIS FRAME — its newest observation, unless recovery has
    /// already succeeded here. <see cref="Handled"/> is what stops it: "recovered, stop being
    /// <c>%!error%</c>". Null when this frame never failed, or failed and was recovered.
    /// <see cref="global::app.call.list.@this.Error"/> walks <see cref="Caller"/> asking each frame this. Kept, it comes
    /// back as the error this frame observed.</summary>
    [Store]
    public global::app.error.@this? Error
    {
        get => _handled ? null : Errors.Newest;
        private set { if (value != null) Errors.Add(value); }
    }

    /// <summary>
    /// Live siblings under this Call. Owns its own lock + FIFO eviction policy — see
    /// <see cref="child.list.@this"/>. Allocated lazily via the constructor below so the
    /// back-reference to the parent CallStack is set before any Add can land.
    /// </summary>
    public child.list.@this Children { get; }

    // --- Timing tier (null when Flags.Timing off) ---
    // The app's type its push handed: when it ran is a value of it (datetime). How long is made as its asker's setting
    // says a span is.
    private readonly global::app.type.@this _datetime;

    /// <summary>When it was pushed. Null when the Timing flag was off at Push.</summary>
    [Out, Debug]
    public global::app.type.item.datetime.@this? StartedAt => _startedAt is { } started ? new(started, _datetime) : null;

    /// <summary>When it was popped. Null while in flight, and when the Timing flag was off.</summary>
    public global::app.type.item.datetime.@this? CompletedAt => _completedAt is { } completed ? new(completed, _datetime) : null;

    /// <summary>Wall time this frame has run — while in flight (an after-binding runs inside the frame) and, once
    /// popped, its whole duration — made in the standard its asker's setting names. Null when the Timing flag was off
    /// at Push.</summary>
    public global::app.type.item.duration.@this? Duration(global::app.actor.context.@this context)
        => _stopwatch is { } watch ? global::app.type.item.duration.@this.Create(watch.Elapsed, context) : null;

    // --- Diff tier (null when Flags.Diff off) ---
    /// <summary>
    /// Variable mutations observed during this Call's lifetime. Null unless Flags.Diff was
    /// on at Push. See <see cref="diff.@this"/> for thread-safety + snapshot iteration.
    /// </summary>
    [Out, Debug, global::app.LlmBuilder]
    public diff.@this? Diffs { get; }

    // --- Tag tier ---
    /// <summary>
    /// Free-form tags written through <see cref="Tag"/> — a plang dict, read as any dict
    /// (<c>%!callStack.Current.Caller.Tags.foo%</c>). Always allocated, so no lazy-init race.
    /// </summary>
    [Store, Out, Debug, global::app.LlmBuilder]
    public global::app.type.item.dict.@this Tags { get; private set; }

    // Parallel branches tag one caller's frame; the frame serializes the merge into its dict.
    private readonly object _tagging = new();

    // This frame's own variables: every name it binds (born with or written into it), and which of them it was born
    // with — what its runner supplied (a goal call's parameters, a tool's arguments).
    private readonly Dictionary<string, global::app.data.@this> _variables = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _arguments = new(StringComparer.OrdinalIgnoreCase);
    // The action this frame was pushed to run (a tool invocation) — the one call its supplied names are FOR.
    private readonly global::app.goal.step.action.@this? _for;

    /// <summary>Whether this frame counts toward the chain's depth (<see cref="Depth"/>, the depth limit) — a goal's,
    /// a step's or an action's does; a frame that only binds names does not.</summary>
    internal virtual bool Deepens => true;

    /// <summary>Whether this frame is timed and keeps its variable changes when the setting asks — a goal's, a step's
    /// or an action's is; a frame that only binds names is not.</summary>
    internal virtual bool Measured => true;

    /// <summary>Whether this is a goal's own frame — its run's scope (<c>%!call.scope%</c>): only a goal's frame
    /// (<see cref="goal.@this"/>) answers yes.</summary>
    internal virtual bool IsGoal => false;

    /// <summary>A frame come back from a snapshot: its stored <paramref name="node"/> (the read copy its async half
    /// filled, <see cref="Find"/>) read whole — the id it was saved with, its goal, step and action where they stood, the
    /// setting it is born with, its own variables and the names it was born with among them, its errors and whether
    /// they were handled, the event running in it, its tags and what it keeps — under <paramref name="stack"/>'s current
    /// frame, its caller, which is back already. Ending it goes back to its caller.</summary>
    private protected @this(global::app.snapshot.@this node, global::app.type.@this type, global::app.call.list.@this stack,
        global::app.actor.context.@this context, global::app.goal.step.action.@this? callee = null)
        : this(Stored<global::app.type.item.text.@this>(node, "id", context).ToString(), type,
            stack.Current, stack.Current, stack, stack.Variables,
            Stored<global::app.call.setting.@this>(node, "setting", context), callee: callee)
    {
        // a collection left out was empty
        if (node.Entries.Get<global::app.snapshot.@this>("variables", context) is { } variables)
            foreach (var variable in variables.In(context)) _variables[variable.Name] = variable;
        foreach (var name in Stored<global::app.type.item.list.@this<global::app.type.item.text.@this>>(node, "arguments", context).Items())
            _arguments.Add(name.ToString());
        foreach (var error in Stored<global::app.type.item.list.@this<global::app.error.@this>>(node, "errors", context).Items())
            Errors.Add(error);
        _handled = Stored<global::app.type.item.@bool.@this>(node, "handled", context).Value;
        Event = node.Entries.Get("event", context);
        if (node.Entries.Get<global::app.type.item.dict.@this>("tags", context) is { } tags)
            foreach (var tag in tags.Entries(context)) Tags.Set(tag);
        if (node.Entries.Get<global::app.type.item.property.list.@this>("property", context) is { } added)
            foreach (var (name, each) in added.Members) property.Add(name, each);
    }

    /// <summary>
    /// Every frame's making: it holds back-references to the owning stack and the frame it goes back to when it ends
    /// (<paramref name="previous"/>, the frame current when it was pushed), so DisposeAsync can restore them and remove
    /// self from Children when history is off. <paramref name="names"/> are the variables it is born with; its
    /// <paramref name="id"/> is the one it was saved with, or a new one (8 hex chars, short enough for log lines).
    /// </summary>
    private protected @this(
        string? id,
        global::app.type.@this type,
        @this? caller,
        @this? previous,
        global::app.call.list.@this stack,
        Variables? diffSource,
        global::app.call.setting.@this setting,
        IEnumerable<global::app.data.@this>? names = null,
        global::app.goal.step.action.@this? callee = null) : base(type)
    {
        _id = id ?? Guid.NewGuid().ToString("N")[..8];
        Setting = setting;
        Caller = caller;
        _depth = (caller?._depth ?? 0) + (Deepens ? 1 : 0);
        _stack = stack;
        _previousCurrent = previous;
        _diffSource = diffSource;
        _for = callee;
        _datetime = stack.Datetime;
        Tags = new global::app.type.item.dict.@this(stack.Dict);
        Errors = new global::app.error.list.@this(stack.Errors);
        property = new(stack.Properties);
        Children = new child.list.@this(stack);
        Variable = names ?? [];

        if (!Measured) return;
        if (setting.Timing.Value)
        {
            _startedAt = DateTimeOffset.UtcNow;
            _stopwatch = Stopwatch.StartNew();
        }

        if (stack.IsDiffing(setting) && diffSource != null)
        {
            Diffs = new diff.@this();
            _deep = setting.Diff.Deep.Value;
            stack.Open(this);
        }
    }

    /// <summary>What the stack captured when this frame was pushed — its async caller read it and handed it in: the
    /// frame's timing and diff, whether it stays among its caller's children, how many of them are kept.</summary>
    internal global::app.call.setting.@this Setting { get; }

    /// <summary>The frame a write to <paramref name="name"/> lands in: this one when it was born with the name, else
    /// the nearest caller that was; null when none was — the write goes to its context's memory.</summary>
    internal virtual @this? Keeper(string name) => _arguments.Contains(name) ? this : Caller?.Keeper(name);

    /// <summary>True when this frame was pushed FOR <paramref name="call"/> and born with <paramref name="name"/> — its
    /// runner supplied it (a tool's argument), so the supplied value wins over the call's own row.</summary>
    public bool Supplies(global::app.goal.step.action.@this call, string name)
        => ReferenceEquals(_for, call) && _arguments.Contains(name);

    /// <summary>What <paramref name="name"/> holds, read from this frame outward — an inner frame's shadows an outer's.
    /// Case-insensitive.</summary>
    public bool TryGet(string name, out global::app.data.@this value)
    {
        for (var frame = this; frame != null; frame = frame.Caller)
            if (frame._variables.TryGetValue(name, out var variable)) { value = variable; return true; }
        value = null!;
        return false;
    }

    /// <summary>The names this frame was born with — a goal call's own arguments, not its callers'.</summary>
    internal IEnumerable<string> Arguments => _arguments;

    /// <summary>This frame's own variables, each the Data whole. Kept, they come back as names it is born with, as a
    /// goal call's parameters are: a write to one lands here again.</summary>
    [Store]
    public IEnumerable<global::app.data.@this> Variable
    {
        get => _variables.Values;
        private set
        {
            foreach (var name in value)
            {
                if (name == null || string.IsNullOrEmpty(name.Name)) continue;
                _variables[name.Name] = name;   // last wins on duplicate names
                _arguments.Add(name.Name);
            }
        }
    }

    /// <summary>The names this frame and its callers hold, the inner frame's first.</summary>
    internal IEnumerable<string> Names
    {
        get
        {
            for (var frame = this; frame != null; frame = frame.Caller)
                foreach (var name in frame._variables.Keys) yield return name;
        }
    }

    /// <summary>Writes <paramref name="value"/> into this frame under <paramref name="name"/> (the frame
    /// <see cref="Keeper"/> chose).</summary>
    public void Set(string name, global::app.data.@this value) => _variables[name] = value;

    /// <summary>True if this frame itself (not its callers) holds <paramref name="name"/>.</summary>
    public bool Holds(string name) => _variables.ContainsKey(name);

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
    /// This frame and its callers, outward: <c>[this, Caller, …, the root]</c> — the frames as they are, not copies,
    /// taken as they stand when asked (an error keeps the chain it failed in). <c>[0]</c> is this frame;
    /// <c>- foreach %!call.current.chain%, call …</c> walks it from plang.
    /// </summary>
    public IReadOnlyList<@this> Chain
    {
        get
        {
            var chain = new List<@this>();
            for (var frame = this; frame != null; frame = frame.Caller) chain.Add(frame);
            return chain;
        }
    }

    /// <summary>This frame and its callers out to its goal's own frame, inclusive — one goal's run, inner first, never
    /// past it to the root.</summary>
    internal IEnumerable<@this> Within
    {
        get
        {
            for (var frame = this; frame != null; frame = frame.Caller)
            {
                yield return frame;
                if (frame.IsGoal) yield break;
            }
        }
    }

    /// <summary>
    /// Length of the synchronous Caller chain rooted at this Call. <c>Root.Depth == 1</c>
    /// (only itself), <c>Root.Children[0].Depth == 2</c>, etc. A birth fact: one more than its
    /// caller's. PLang tests can <c>assert %!callStack.Current.Depth% equals 2</c>.
    /// </summary>
    [Out, Debug, global::app.LlmBuilder]
    public global::app.type.item.number.@this Depth => _depth;

    /// <summary>The frame records an error against itself — the one door. Stamps what the error does
    /// not carry yet: the failing chain, the context of the run it met here, and — when the error keeps
    /// them (<c>Keeps</c>: an assertion, or any error under --debug) — the variables as they are now. Then
    /// files it on the frame and in the run's audit.
    /// <para>Recording each error ONCE is the frame's own contract, kept by instance identity, so no
    /// caller guards: a retry mints a fresh error per attempt and each is kept (real history), while
    /// a layer that passes the same error through records nothing new.</para></summary>
    public void Record(global::app.error.@this error, actor.context.@this context)
    {
        if (error.CallFrames.Count == 0) error.CallFrames = Chain;
        error.Step ??= Step;
        error.Context ??= context;
        if (error.Variables == null && error.Keeps(context)) error.Variables = context.Variable.Snapshot();
        if (Errors.Any(x => ReferenceEquals(x, error))) return;
        Errors.Add(error);
        _stack.Audit.Add(error);
    }

    // Where the action stands in its step; -1 when the frame runs no action, or its step doesn't hold it.
    private protected int Place => Action != null && Step != null ? Step.Code.IndexOf(Action) : -1;

    /// <summary>The action's index in its step; -1 when the frame runs no action, or its step doesn't hold it.</summary>
    public global::app.type.item.number.@this Index => Place;

    /// <summary>A resume point: an action its step holds. A goal's or a step's frame is not one, nor an action
    /// composed in C#, which no step holds — the resumed run makes those again.</summary>
    public global::app.type.item.@bool.@this IsResumable => Place >= 0;

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
        _ended = true;
        if (_stopwatch != null)
        {
            _stopwatch.Stop();
            _completedAt = DateTimeOffset.UtcNow;
        }

        if (Diffs != null) _stack.Close(this);

        if (!Setting.History.Value && Caller != null)
            Caller.Children.Remove(this);

        // AsyncLocal restore: only flip back if we're still the Current. If a parallel branch
        // has its own Current, we leave that alone.
        _stack.RestoreCurrent(this, _previousCurrent);

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Capture rule for diff Before values. Scalars (int/bool/decimal/DateTimeOffset/short
    /// strings) pass through; non-scalars become summary strings unless diff.deep is on,
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
