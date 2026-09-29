using System.Text.Json.Serialization;
namespace app.goal.step.action;

/// <summary>
/// A single action within a step — the LLM-mapped unit of execution.
/// Identifies the module and handler to invoke, with typed parameters, return mappings, and defaults.
/// </summary>
public partial class @this
{
    public static string Example => "goal.call(Name=\"Show\")";

    // An action is a plain C# host — carried as clr<action>, reflected off its [Store] props.

    private global::app.module.@this? _module;

    /// <summary>The module this action belongs to — the element itself, not its name. Every
    /// construction door sets it; reading it unset means the action was built outside a door,
    /// which is a bug, so it throws rather than answering a half-built identity. Carries [Debug]
    /// only — the wire form is written explicitly by <c>Output</c>.</summary>
    [Debug]
    public global::app.module.@this Module
    {
        get => _module ?? throw new System.InvalidOperationException(
            $"action '{Name}' has no module — it was constructed outside a construction door.");
        set => _module = value;
    }

    [Store, LlmBuilder, Debug, Default]
    [JsonPropertyName("action")]
    [Newtonsoft.Json.JsonProperty("action")]
    public string Name { get; set; } = "";

    /// <summary>The action's own name — "read". It never borrows the module's identity; a site that
    /// wants the qualified form composes the two objects (<c>$"{a.Module}.{a.Name}"</c>).</summary>
    public override string ToString() => Name;

    /// <summary>The action's properties. A program action's are what its step set (the .pr's
    /// <c>"property"</c>); a catalog action's (<c>module[name]</c>) are its handler class's, reflected.
    /// Which one is decided by the list's constructor — the catalog element is born with its own.</summary>
    [JsonIgnore]
    public global::app.type.property.list.@this Property { get; init; } = new();

    /// <summary>What the build froze for the properties the step did not set (the .pr's
    /// <c>"default"</c>) — frozen so a later runtime that changes a <c>[Default]</c> runs the built
    /// program the same.</summary>
    [JsonIgnore]
    public global::app.type.property.list.@this Default { get; init; } = new();

    /// <summary>The branch body of a control-flow action (the steps that run when this condition fires).
    /// Empty on every non-control-flow action — the fire gate is <c>Child.Count &gt; 0 &amp;&amp; truthy</c>.
    /// Both nesting forms land here: inline <c>if/elseif/else</c> (each condition action carries its body)
    /// and indented sub-step blocks (folded onto the gate action). A <c>step.list</c>, so it runs itself.</summary>
    [Store, Debug, Default]
    public global::app.goal.step.list.@this Child { get; set; } = new();

    [Debug]
    public global::app.warning.list.@this Warning { get; init; } = new();

    // `new`: this is the ACTION-cache flag (may this action's run result be
    // cached), a distinct concept from the item base's answer-keep rule —
    // which never applies here (an action's Ready() answers itself).
    [JsonIgnore]
    public new bool Cacheable { get; init; } = true;

    /// <summary>The class that runs this action — held by the module's catalog action, which is born from it; a
    /// program's action asks its module (<c>Module.Create</c>).</summary>
    [JsonIgnore]
    internal System.Type? Class { get; init; }

    /// <summary>
    /// True when this action was constructed inline in C# (default for
    /// <c>new SomeAction { ... }</c>). False when materialized from a .pr
    /// file. CallStack.Push stamps the Call frame; wire-serialize filters
    /// synthetic frames out of the Snapshot (they can't be restored from PR
    /// and are recreated naturally by the resumed execution). PR-load sites
    /// override this to <c>false</c> when materializing the action from JSON.
    /// </summary>
    [JsonIgnore]
    public bool Synthetic { get; set; } = true;

    /// <summary>
    /// Pre-built handler instance for inline C# composition. When set,
    /// <see cref="DispatchAsync"/> uses it directly instead of resolving via
    /// <c>Modules.GetCodeGenerated</c>. Null on PR-loaded actions (the dispatch
    /// path resolves a fresh handler per execution).
    /// </summary>
    [JsonIgnore]
    /// <summary>
    /// A C#-composed action carrying provided params (via <c>app.Run</c>). Dispatch runs the
    /// normal path; the generated Resolve passes through the seed's *set* params (no round-trip)
    /// while filling the UNSET ones from setting → [Default]. Null on the .pr path.
    /// </summary>
    public module.ICodeGenerated? Seed { get; init; }

    /// <summary>An action read or built field by field (a <c>.pr</c> row, a catalog element).</summary>
    public @this() { }

    /// <summary>
    /// The action a C#-composed <paramref name="seed"/> is — an operation one owner composes from another's
    /// (signing hashes, llm sends its request through http), run as an action so it stays observable: its
    /// frame, its <c>on.start</c>, a binding a program (or a mock) puts on it. Its module and name are the
    /// catalog's — the element registration made for the seed's class; the seed's set parameters pass through,
    /// the unset resolve from setting and <c>[Default]</c>. Born knowing the step that invoked it: composed and
    /// started together, the calling frame is this invocation's provenance (null at the boot edge, before any
    /// goal runs).
    /// </summary>
    public @this(module.ICodeGenerated seed, actor.context.@this context)
    {
        var catalog = context.App.module[seed.GetType()]
            ?? throw new System.InvalidOperationException($"no module holds the action {seed.GetType().Name}");
        Module = catalog.Module;
        Name = catalog.Name;
        Seed = seed;
        Step = context.CallStack.Step;
    }

    /// <summary>
    /// True for any condition chain action: condition.if, condition.elseif, or condition.else.
    /// Used by the condition.Decision type to split an orchestrated step's actions into per-branch
    /// groups.
    /// </summary>
    [JsonIgnore]
    public bool IsCondition =>
        string.Equals(Module.Name, "condition", StringComparison.OrdinalIgnoreCase) &&
        (string.Equals(Name, "if", StringComparison.OrdinalIgnoreCase)
      || string.Equals(Name, "elseif", StringComparison.OrdinalIgnoreCase)
      || string.Equals(Name, "else", StringComparison.OrdinalIgnoreCase));

    /// <summary>A condition action that continues a chain an if opened — an elseif or an else. It
    /// follows an if (or an elseif) in the same step, never stands first.</summary>
    [JsonIgnore]
    public bool IsBranch => IsCondition && !string.Equals(Name, "if", StringComparison.OrdinalIgnoreCase);

    /// <summary>Where this action stands in a condition chain: the if 0, an elseif 1, the else 2 (it
    /// closes the chain); null for an action that isn't a condition.</summary>
    [JsonIgnore]
    public int? Link => !IsCondition ? null
        : !IsBranch ? 0
        : string.Equals(Name, "else", StringComparison.OrdinalIgnoreCase) ? 2 : 1;

    /// <summary>The step this action belongs to — a BIRTH FACT for every action that is part of a
    /// PROGRAM: the reader builds the step shell first and hands it down at construction, so it is
    /// never stamped in afterwards. Null is not a repair hole, it is a real state: two kinds of
    /// action exist outside any program and therefore have no step — a catalog element (a
    /// module-minted descriptor), and a synthetic action composed in C# (<c>app.Run(new sign{...})</c>,
    /// the signing/verify/ask seam).</summary>
    [JsonIgnore]
    public Step? Step { get; init; }

    // Teaching prose (Description / Notes / Examples) is no longer stored on the action host — it lives
    // as lazy `file` handles on the class-zoom partial (this.Schema.cs), over
    // os/system/modules/{Module}/{Name}.{facet}.md. Templates read module-first + action through
    // those doors; the old string fields + MergeLayers `*Rendered` cousins are retired.


    /// <summary>
    /// The property the step set, by name; null when the step did not set it. The program is
    /// shared by every run: a run makes its own Data from the property (<c>Data(context)</c>).
    /// </summary>
    public global::app.type.property.@this? this[string name] => Property[name];

    /// <summary>An action starts through four levels, the general wrapping the specific: the action type's
    /// events, its module's, its catalog action's (when it is another object than this one), its own.</summary>
    protected internal override global::app.type.item.@this? Level(int depth, actor.context.@this context)
    {
        var catalog = Module[Name] is { } found && !ReferenceEquals(found, this) ? found : null;
        return depth switch
        {
            0 => context.App.action,
            1 => Module,
            2 => catalog ?? this,
            3 => catalog != null ? this : null,
            _ => null,
        };
    }

    /// <summary>
    /// Starts this action through its <c>on.start</c> (<see cref="Level"/>): what is bound before it, then its
    /// dispatch, then what is bound after it. A before that fails or cancels is the result; every after still
    /// runs on it.
    /// Context travels as parameter — actions are shared objects, not per-request.
    /// Owns its own callstack push/pop, anchor save/restore and exception translation.
    /// </summary>
    public async Task<global::app.data.@this> Start(actor.context.@this context)
    {
        // ONE FRAME PER ACTION. The frame spans the action's whole run — its lifecycle events,
        // its modifiers, and its dispatch — not just the dispatch. A modifier recovering from a
        // failure (on.error) therefore runs INSIDE the frame that failed, which is where the
        // error already is: CallStack.Error / %!error% read it off the live chain, and marking it
        // Handled on that frame is what takes it out of play. When the Push wrapped dispatch only,
        // the frame died between the failure and the recovery that had to see it.
        global::app.callstack.call.@this call;
        try { call = context.CallStack.Push(this, context.Variable); }
        catch (global::app.error.CallStackOverflowException ex)
        {
            // The depth limit — trips at Push, before the frame is on the
            // stack, so the contract (returns Data, never throws) is held here.
            return context.Error(context.CallStack.Overflow(ex, Step?.Goal, Step));
        }
        await using var _call = call;

        var data = await Attempt(context);

        // The error outcome — after the attempt's own after-start, so a cache stores only the real work and a
        // recovery runs outside the attempt's deadline. The first clause bound on it that takes the failure
        // answers (a retry, a recovery, an ignore); a failure no clause takes stands.
        if (!data.Success) data = await on.error.Catch(this, data, context);

        // %!data% is the last action's result, stored AS-IS. A reference stays a
        // reference and a lazy source stays unread — %!data% never forces a value.
        // Resolution happens only when a real consumer opens the door; storing the
        // value here would read a pending file / resolve a %ref% at every action.
        if (data.Success)
            await context.Variable.Set("!data", data);
        return data;
    }

    /// <summary>
    /// One attempt of this action, inside the frame <see cref="Start"/> pushed for it: what is bound before its
    /// start, its dispatch, what is bound after it. A before that fails or cancels is the attempt's result, and
    /// every after still runs on it. A retry (<c>on.error</c>) is another attempt in the same frame — a fresh
    /// deadline, a fresh cache lookup.
    /// </summary>
    internal async Task<global::app.data.@this> Attempt(actor.context.@this context)
    {
        var answer = await on.start.Before(this, context);
        global::app.data.@this data;
        if (answer is { Handled: true })
        {
            // Cancelled: a before-binding's answer is this action's result (a mock, a cache hit, on.cancel).
            // Clear Handled so the outer step loop doesn't misread "dispatch was short-circuited" as "stop the
            // step" — the next action in the chain still needs to run on this result.
            data = answer;
            data.Handled = false;
        }
        else if (answer is { Success: false })
            data = answer;
        else
            data = await DispatchAsync(context, context.CallStack.Current!);
        return await on.start.After(this, data, context);
    }

    /// <summary>A program action of this catalog element's kind, in <paramref name="step"/> — a clause when this
    /// one is (the role was decided when its module registered it).</summary>
    internal virtual @this Program(global::app.goal.step.@this? step)
        => new() { Module = Module, Name = Name, Step = step, Synthetic = false };

    // A step's code is a walk of its actions, each answering its own part. A step action runs and anchors
    // what follows it; a clause (clause.@this) overrides each part.

    /// <summary>Attaches this action to <paramref name="before"/>, the step action it follows — what the program's
    /// read does once a step's code is in. A step action attaches to nothing.</summary>
    internal virtual void Attach(@this? before) { }

    /// <summary>The step action the actions after this one belong to: this one.</summary>
    internal virtual @this? Anchor(@this? before) => this;

    /// <summary>This action's turn in its step's chain, after <paramref name="result"/>: it starts, and its result
    /// is the chain's.</summary>
    internal virtual Task<global::app.data.@this> Follow(global::app.data.@this result, actor.context.@this context)
        => Start(context);

    /// <summary>What the build refuses in this action as it follows <paramref name="before"/>: nothing.</summary>
    internal virtual global::app.error.Error? Refuse(@this? before) => null;

    /// <summary>This catalog action's place in a step's pre-filled formal: a step action after the ones before
    /// it; an if opens the body the step's other actions go into, when the line nests. One whose Start declares
    /// a value (not a bare <c>item</c>) produces what a keep in the same step keeps — but not a condition, whose
    /// verdict gates its body and is never kept.</summary>
    internal virtual void Prefill(global::app.goal.step.pick.line.@this line, string call)
        => line.Add(call, opens: Link == 0, produces: Link == null && Return is { } made && made.Name != "item");

    /// <summary>What this catalog action adds to a step's known code: its call.</summary>
    internal virtual void Know(List<string> line, string call) => line.Add(call);

    /// <summary>Why this action takes no <c>{ }</c> body; null for a condition, whose body it is.</summary>
    internal virtual string? Nest() => IsCondition ? null
        : $"`{Module.Name}.{Name}` contains no actions: only a condition takes {{ }} (its body); " +
          $"write the actions one after the other: {Module.Name}.{Name}(…); next.action(…)";

    /// <summary>
    /// Dispatches this action inside the frame <see cref="Start"/> pushed for it: mints its handler,
    /// resolves it, runs it, and records a failure on the frame. The frame is NOT created here — a
    /// retry dispatches again into the same frame, and a modifier recovering from a failure is still
    /// inside it.
    /// <para>Deliberately catches OperationCanceledException — timeout.after depends on this: the
    /// inner action's generated Start swallows OCE into a ServiceError result, so timeout.after
    /// detects the timeout via CTS state + failed result; this catch is the safety net for a handler
    /// that bubbles it differently. Step.Start's catch DOES exclude OCE — that asymmetry is intentional.</para>
    /// </summary>
    private async Task<global::app.data.@this> DispatchAsync(
        actor.context.@this context, global::app.callstack.call.@this call)
    {
        // Uniform dispatch: always resolve the shell + run Resolve (the seam). A C#-composed
        // Seed (app.Run) rides on the entity and is read by the generated Resolve as the
        // pass-through for its set params — no separate skip-Resolve path.
        var (code, error) = Instance(context);
        if (error != null) return context.Error(error);

        // `code` is the throwaway registry shell; Resolve builds the fresh, populated instance
        // that runs. `real` is kept for the catch path's parameter snapshot.
        module.ICodeGenerated? real = null;
        try
        {
            var (resolved, resolveErr) = await code!.Resolve(this, context);
            if (resolveErr != null)
            {
                call.Record(resolveErr, context);
                return context.Error(resolveErr);
            }
            real = resolved;
            var result = await real!.Start();
            // The handler's parameters ride on its error, snapshotted here, not in the handler.
            if (!result.Success && result.Error is { } err)
            {
                err.Params ??= real.SnapshotParams();
                call.Record(err, context);
            }
            return result;
        }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            // A typed AppException carries a domain Key (VariableNotFound, GoalNotFound, …) —
            // kept; a bare exception defaults to ServiceError.
            var appEx = ex as global::app.error.AppException;
            var serviceErr = new global::app.error.ServiceError(
                ex.Message, Step!, call.SnapshotChain(), appEx?.Key ?? "ServiceError", appEx?.StatusCode ?? 400) { Exception = ex };
            serviceErr.Params = real?.SnapshotParams();
            call.Record(serviceErr, context);
            return context.Error(serviceErr);
        }
    }

    /// <summary>This action, instantiated — the live object carrying its typed parameters and
    /// Run. The action asks the module element it HOLDS for its own name; no registry re-resolves
    /// strings. A name the module doesn't carry, or one whose entry isn't code-generated, comes
    /// back as a keyed error.</summary>
    public (module.ICodeGenerated? Code, global::app.error.Error? Error) Instance(
        actor.context.@this context)
    {
        if (!Module.Contains(Name))
            return (null, global::app.error.ActionError.NotFound($"Action '{Module}.{Name}'"));

        var code = Module.Create(Name, context);
        return code == null
            ? (null, new global::app.error.ActionError(
                $"Action '{Module}.{Name}' does not implement ICodeGenerated", "ActionError", 500))
            : (code, null);
    }

    /// <summary>This action bound: its handler minted and its parameters bound as typed views —
    /// nothing resolved. What the build pass needs to ask the handler, and what a runner needs to read
    /// a held action's own properties.</summary>
    public async Task<(module.ICodeGenerated? Handler, global::app.error.Error? Error)> Bind(
        actor.context.@this context)
    {
        var (code, error) = Instance(context);
        if (error != null) return (null, error);
        return await code!.Resolve(this, context);
    }

    /// <summary>
    /// PLang name of the action's return type T (when Start() returns Task&lt;Data&lt;T&gt;&gt;).
    /// Null when Start() returns bare <c>Task&lt;Data&gt;</c> — i.e. void: the action has no
    /// meaningful value to write to a variable. Compile.llm uses this to choose the Type
    /// for a trailing <c>variable.set</c> after a <c>write to %x%</c>.
    /// </summary>

}
