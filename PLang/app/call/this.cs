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

    private Stopwatch? _stopwatch;
    private readonly global::app.call.list.@this _stack;
    private readonly @this? _previousCurrent;
    // The variables whose changes this frame captures as its diffs (none: it captures none).
    private readonly Variables? _store;
    // A diff keeps a deep copy of the value before (the diff.deep setting when pushed), else a summary.
    private bool _deep;
    private Dictionary<global::System.Type, object>? _items;

    // The frame's facts as it holds them — a frame is pushed per action, so its plang faces below are made
    // when read, never at push.
    private readonly string _id;
    internal readonly int _depth;
    private bool _handled;
    private bool _ended;
    private DateTimeOffset? _startedAt;
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
    // When it ran is a value of its stack's datetime type, how long a span, written in the standard its asker's setting
    // names.

    /// <summary>When it was pushed. Null when the Timing flag was off at Push.</summary>
    [Out, Debug]
    public global::app.type.item.datetime.@this? StartedAt => _startedAt is { } started ? new(started, _stack.Datetime) : null;

    /// <summary>When it was popped. Null while in flight, and when the Timing flag was off.</summary>
    public global::app.type.item.datetime.@this? CompletedAt => _completedAt is { } completed ? new(completed, _stack.Datetime) : null;

    /// <summary>Wall time this frame has run — while in flight (an after-binding runs inside the frame) and, once
    /// popped, its whole duration — a span, written in the standard its asker's setting names. Null when the Timing flag
    /// was off at Push.</summary>
    [Out, Debug]
    public global::app.type.item.duration.@this? Duration
        => _stopwatch is { } watch ? new global::app.type.item.duration.span.@this(watch.Elapsed, _stack.Span) : null;

    // --- Diff tier (null when Flags.Diff off) ---
    /// <summary>
    /// Variable mutations observed during this Call's lifetime. Null unless Flags.Diff was
    /// on at Push. See <see cref="diff.@this"/> for thread-safety + snapshot iteration.
    /// </summary>
    [Out, Debug, global::app.LlmBuilder]
    public diff.@this? Diffs { get; private set; }

    // --- Tag tier ---
    /// <summary>
    /// Free-form tags written through <see cref="Tag"/> — a plang dict, read as any dict
    /// (<c>%!callStack.Current.Caller.Tags.foo%</c>). Always allocated, so no lazy-init race.
    /// </summary>
    [Store, Out, Debug, global::app.LlmBuilder]
    public global::app.type.item.dict.@this Tags { get; private set; }

    // Parallel branches tag one caller's frame; the frame serializes the merge into its dict.
    private readonly object _tagging = new();

    // Which of this frame's own variables (Parameter) it was born with — what its runner supplied (a goal call's
    // parameters, a tool's arguments).
    private readonly HashSet<string> _arguments = new(StringComparer.OrdinalIgnoreCase);
    // The call this frame was pushed for (a tool invocation, a goal call) — the one call its supplied names are for.
    private readonly global::app.goal.step.action.@this? _call;

    /// <summary>Whether this frame counts toward the chain's depth (<see cref="Depth"/>, the depth limit) — a goal's,
    /// a step's or an action's does; a frame that only binds names does not.</summary>
    internal virtual bool Deepens => true;

    /// <summary>Whether this frame is timed and keeps its variable changes when the setting asks — a goal's, a step's
    /// or an action's is; a frame that only binds names is not.</summary>
    internal virtual bool Measured => true;

    /// <summary>Whether this is a goal's own frame — its run's scope (<c>%!call.scope%</c>): only a goal's frame
    /// (<see cref="goal.@this"/>) answers yes.</summary>
    internal virtual bool IsGoal => false;

    /// <summary>A frame come back from a snapshot, made, not entered: born under <paramref name="stack"/>'s current
    /// frame, its caller, which is entered already, and its kept members read onto it as the snapshot wrote them — its
    /// variables (the names it is born with), the error in play, the event running in it, its tags and what a program
    /// added. Its element its own kind reads (<see cref="Kept{T}"/>). The stack enters it.</summary>
    private protected @this(global::app.type.item.dict.@this record, global::app.type.@this type, global::app.call.list.@this stack,
        global::app.actor.context.@this context)
        : this(null, type, stack.Current, stack.Current, stack, null)
        => ((global::app.type.item.kind.reflection.@this)context.App.type.list["item"].kind["*"]!)
            .Read(record, this, context, global::app.View.Snapshot);

    /// <summary>The element a stored frame's <paramref name="record"/> keeps under <paramref name="name"/> — a goal, a
    /// step, an action — read in the snapshot view: the live one its key finds. A key that finds none, or a record that
    /// keeps none, is refused, thrown to the read boundary: a frame is never born without its element.</summary>
    private protected T Kept<T>(global::app.type.item.dict.@this record, string name, global::app.actor.context.@this context)
        where T : global::app.type.item.@this
        => global::app.View.Snapshot.Make(context.App.type.list[typeof(T)], record.Get(name, context)?.Peek(), context) as T
           ?? throw new global::app.error.DeclinedException(new global::app.error.@this(
               $"a saved {Type.Name} frame keeps no {name}", "FrameKeyIncomplete", 400));

    /// <summary>
    /// Every frame's making: it holds back-references to the owning stack and the frame it goes back to when it ends
    /// (<paramref name="previous"/>, the frame current when it was pushed), so DisposeAsync can restore them and remove
    /// self from Children when history is off. <paramref name="names"/> are the variables it is born with; its
    /// <paramref name="id"/> is the one it was saved with, or a new one (8 hex chars, short enough for log lines);
    /// <paramref name="store"/> the variables whose changes it captures; <paramref name="call"/> the call it was pushed for.
    /// </summary>
    private protected @this(
        string? id,
        global::app.type.@this type,
        @this? caller,
        @this? previous,
        global::app.call.list.@this stack,
        Variables? store,
        IEnumerable<global::app.data.@this>? names = null,
        global::app.goal.step.action.@this? call = null) : base(type)
    {
        _id = id ?? Guid.NewGuid().ToString("N")[..8];
        Caller = caller;
        _depth = (caller?._depth ?? 0) + (Deepens ? 1 : 0);
        _stack = stack;
        _previousCurrent = previous;
        _store = store;
        _call = call;
        Tags = new global::app.type.item.dict.@this(stack.Dict);
        Errors = new global::app.error.list.@this(stack.Errors);
        property = new(stack.Properties);
        Children = new child.list.@this(stack);
        _parameter = new(stack.Properties);
        foreach (var name in names ?? [])
            if (name != null && !string.IsNullOrEmpty(name.Name)) Born(name.Name, name);
    }

    /// <summary>
    /// The frame's run begins, as its stack enters it: it runs under <paramref name="setting"/>, timed and capturing
    /// its store's changes when the setting says so. A frame made and never entered has no run, so nothing it writes
    /// reads the setting.
    /// </summary>
    internal void Begin(global::app.call.setting.@this setting)
    {
        Setting = setting;
        if (!Measured) return;
        if (setting.Timing.Value)
        {
            _startedAt = DateTimeOffset.UtcNow;
            _stopwatch = Stopwatch.StartNew();
        }

        if (_stack.IsDiffing(setting) && _store != null)
        {
            Diffs = new diff.@this();
            _deep = setting.Diff.Deep.Value;
            _stack.Open(this);
        }
    }

    /// <summary>What the stack captured when this frame's run began — its async caller read it and handed it in: the
    /// frame's timing and diff, whether it stays among its caller's children, how many of them are kept.</summary>
    internal global::app.call.setting.@this Setting { get; private set; } = null!;

    /// <summary>The frame a write to <paramref name="name"/> lands in: this one when it was born with the name, else
    /// the nearest caller that was; null when none was — the write goes to its context's memory.</summary>
    internal virtual @this? Keeper(string name) => _arguments.Contains(name) ? this : Caller?.Keeper(name);

    /// <summary>True when this frame was pushed FOR <paramref name="call"/> and born with <paramref name="name"/> — its
    /// runner supplied it (a tool's argument), so the supplied value wins over the call's own row.</summary>
    public bool Supplies(global::app.goal.step.action.@this call, string name)
        => ReferenceEquals(_call, call) && _arguments.Contains(name);

    /// <summary>What <paramref name="name"/> holds, read from this frame outward — an inner frame's shadows an outer's.
    /// Case-insensitive.</summary>
    public bool TryGet(string name, out global::app.data.@this value)
    {
        for (var frame = this; frame != null; frame = frame.Caller)
            if (frame._parameter[name] is { } variable) { value = variable; return true; }
        value = null!;
        return false;
    }

    /// <summary>The names this frame was born with — a goal call's own arguments, not its callers'.</summary>
    internal IEnumerable<string> Arguments => _arguments;

    /// <summary>This frame's own variables, each the Data whole under its name (<c>%!call.scope.parameter.order%</c>) —
    /// the list itself, so what is written through it is the frame's. Kept, they come back as names it is born with,
    /// as a goal call's parameters are: a write to one lands here again.</summary>
    [Store, global::app.LlmBuilder]
    public global::app.type.item.property.list.@this Parameter
    {
        get => _parameter;
        private set
        {
            _parameter = value;
            foreach (var (name, _) in value.Members) _arguments.Add(name);
        }
    }
    private global::app.type.item.property.list.@this _parameter;

    // A name this frame is born with: bound here, and one of the names it was born with (last wins on a duplicate).
    private void Born(string name, global::app.data.@this variable)
    {
        _parameter.Add(name, variable);
        _arguments.Add(name);
    }

    /// <summary>The names this frame and its callers hold, the inner frame's first.</summary>
    internal IEnumerable<string> Names
    {
        get
        {
            for (var frame = this; frame != null; frame = frame.Caller)
                foreach (var (name, _) in frame._parameter.Members) yield return name;
        }
    }

    /// <summary>Writes <paramref name="value"/> into this frame under <paramref name="name"/> (the frame
    /// <see cref="Keeper"/> chose).</summary>
    public void Set(string name, global::app.data.@this value) => _parameter.Add(name, value);

    /// <summary>True if this frame itself (not its callers) holds <paramref name="name"/>.</summary>
    public bool Holds(string name) => _parameter[name] != null;

    /// <summary>
    /// A change of <paramref name="store"/>: when it is the store this frame captures, the diff lands here —
    /// <paramref name="name"/> held <paramref name="before"/>, null when it was new, so reverse-apply unwinds
    /// the create. Parallel branches sharing the store record concurrently; Diffs owns its lock.
    /// </summary>
    internal void Record(Variables store, string name, object? before)
    {
        if (ReferenceEquals(store, _store))
            Diffs?.Add(new Diff(name, before, _deep));
    }

    /// <summary>The changes this frame and every frame under it captured later than <paramref name="t"/>, each frame
    /// once (<paramref name="seen"/>).</summary>
    internal IEnumerable<Diff> Since(DateTimeOffset t, HashSet<@this> seen)
    {
        // a frame's own diffs once (one already yielded is in seen); the frames under it still walked
        if (seen.Add(this) && Diffs is { } diffs)
            foreach (var diff in diffs.Since(t))
                yield return diff;
        foreach (var child in Children)
            foreach (var diff in child.Since(t, seen))
                yield return diff;
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
        error.At(this, context);
        if (Errors.Any(x => ReferenceEquals(x, error))) return;
        Errors.Add(error);
        _stack.Audit.Add(error);
    }


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
        _stack.Leave(this, _previousCurrent);

        return ValueTask.CompletedTask;
    }

    /// <summary>The pure core — a frame passes through; nothing else converts into one.</summary>
    public static @this? Create(object? raw) => raw as @this;

    /// <summary>A frame passes through; a stored frame comes back through its own kind's Create (a goal's, a step's, an
    /// action's, a binding's, an isolated one), the type it was stored as.</summary>
    public static @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data) => raw as @this;

    /// <summary>Whether it has ended — a frame kept among its caller's children after that is history, not state.</summary>
    internal bool Ended => _ended;

    /// <summary>Whether it is one of <paramref name="stack"/>'s frames — a task's first frame sits among its caller's
    /// children but belongs to the task's own stack.</summary>
    internal bool Of(global::app.call.list.@this stack) => ReferenceEquals(_stack, stack);

    /// <summary>
    /// What is left of this frame's run once it has ended and what it called has returned, run in its caller — the
    /// call stack's resume goes outward from the frame the run stopped in, each frame in turn doing its own rest
    /// (<paramref name="again"/>: this is the frame the run stopped in). A frame of its own kind says what its rest is
    /// (an action's, a step's); any other leaves nothing — its caller goes on.
    /// </summary>
    internal virtual System.Threading.Tasks.Task<data.@this> Rest(actor.context.@this context, bool again)
        => System.Threading.Tasks.Task.FromResult(context.Ok());
}
