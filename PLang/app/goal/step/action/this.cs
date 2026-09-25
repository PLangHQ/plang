using System.Text.Json.Serialization;
using app.variable;
namespace app.goal.step.action;

/// <summary>
/// A single action within a step — the LLM-mapped unit of execution.
/// Identifies the module and handler to invoke, with typed parameters, return mappings, and defaults.
/// </summary>
public partial class @this
{
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

    /// <summary>The modifiers wrapping this action (cache.wrap, error.handle, timeout.after), outermost
    /// first — the written order. The list owns how they compose around the action (<c>Modifier.Wrap</c>).</summary>
    [Store, Debug, Default]
    public modifier.list.@this Modifier { get; init; } = new();

    /// <summary>The branch body of a control-flow action (the steps that run when this condition fires).
    /// Empty on every non-control-flow action — the fire gate is <c>Child.Count &gt; 0 &amp;&amp; truthy</c>.
    /// Both nesting forms land here: inline <c>if/elseif/else</c> (each condition action carries its body)
    /// and indented sub-step blocks (folded onto the gate action). A <c>step.list</c>, so it runs itself.</summary>
    [Store, Debug, Default]
    public global::app.goal.step.list.@this Child { get; set; } = new();

    /// <summary>The actions that run when this one's recovery fires — the body of an `on error`
    /// clause. Empty on every action but <c>error.handle</c>. A structural slot like
    /// <see cref="Modifier"/> and <see cref="Child"/>, not a parameter value: an action is program,
    /// not data, so it is read at load through the same door as any other action and is born
    /// holding the enclosing step. A <c>action.list</c>, so it runs itself.</summary>
    [Store, Debug, Default]
    public global::app.goal.step.action.list.@this Recovery { get; set; } = new();

    [Debug]
    public global::app.warning.list.@this Warning { get; init; } = new();

    // `new`: this is the ACTION-cache flag (may this action's run result be
    // cached), a distinct concept from the item base's answer-keep rule —
    // which never applies here (an action's Ready() answers itself).
    [JsonIgnore]
    public new bool Cacheable { get; init; } = true;

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

    /// <summary>The step this action belongs to — a BIRTH FACT for every action that is part of a
    /// PROGRAM: the reader builds the step shell first and hands it down at construction, so it is
    /// never stamped in afterwards. Null is not a repair hole, it is a real state: three kinds of
    /// action exist outside any program and therefore have no step — a catalog element (a
    /// module-minted descriptor), a synthetic action composed in C# (<c>app.Run(new sign{...})</c>,
    /// the signing/verify/ask seam), and — until recovery moves into <c>Child</c> — a recovery
    /// action materialised from a parameter value.</summary>
    /// <remarks><c>internal set</c>, not <c>init</c>, for exactly one more step: <c>error.handle</c>
    /// must hand recovery actions the enclosing step. When recovery is read at load like every
    /// other action, that last stamp goes and this tightens to <c>init</c>.</remarks>
    [JsonIgnore]
    public Step? Step { get; internal set; }

    private module.Events? _events;
    [JsonIgnore]
    public module.Events Events
    {
        get => _events ??= new module.Events(this);
    }

    // Teaching prose (Description / Notes / Examples) is no longer stored on the action host — it lives
    // as lazy `file` handles on the class-zoom partial (this.Schema.cs), over
    // os/system/modules/{Module}/{Name}.{facet}.md. Templates read module-first + action through
    // those doors; the old string fields + MergeLayers `*Rendered` cousins are retired.


    /// <summary>
    /// The property the step set, by name; null when the step did not set it. The program is
    /// shared by every run: a run makes its own Data from the property (<c>Data(context)</c>).
    /// </summary>
    public global::app.type.property.@this? this[string name] => Property[name];

    /// <summary>
    /// Runs this action: lifecycle events → dispatch → return mapping.
    /// Context travels as parameter — actions are shared objects, not per-request.
    /// Owns its own callstack push/pop, anchor save/restore, exception translation
    /// (formerly App.Run's body — collapsed in stage 2a.5 since "action owns its
    /// execution").
    /// </summary>
    public async Task<global::app.data.@this> Run(actor.context.@this context)
    {
        // ONE FRAME PER ACTION. The frame spans the action's whole run — its lifecycle events,
        // its modifiers, and its dispatch — not just the dispatch. A modifier recovering from a
        // failure (error.handle) therefore runs INSIDE the frame that failed, which is where the
        // error already is: CallStack.Error / %!error% read it off the live chain, and marking it
        // Handled on that frame is what takes it out of play. When the Push wrapped dispatch only,
        // the frame died between the failure and the recovery that had to see it.
        global::app.callstack.call.@this call;
        try { call = context.CallStack.Push(this, context.Variable); }
        catch (global::app.error.CallStackOverflowException ex)
        {
            // Depth limit or ContainsGoal cycle — trips at Push, before the frame is on the
            // stack, so the contract (returns Data, never throws) is held here.
            var caller = context.CallStack.Current;
            var chain = caller != null ? caller.SnapshotChain() : Array.Empty<global::app.callstack.call.@this>();
            var overflowErr = new global::app.error.ServiceError(ex.Message, this.Step!, chain, "CallStackOverflow", 500) { Exception = ex };
            context.CallStack.Audit.Add(overflowErr);
            return context.Error(overflowErr);
        }
        await using var _call = call;

        var lifecycle = context.LifecycleFor(this);

        var beforeResult = await lifecycle.Before.Run(context, new app.@event.moment.@this(app.@event.Trigger.BeforeAction, this));
        if (!beforeResult.Success) return beforeResult;

        global::app.data.@this data;
        if (beforeResult.Handled)
        {
            // Override path: the BeforeAction binding supplied this action's result
            // (mock.intercept, event.skipAction). Clear Handled so the outer step
            // loop doesn't misread "dispatch was short-circuited" as "stop the step" —
            // the next action in the chain still needs to run on this result.
            data = beforeResult;
            data.Handled = false;
        }
        else if (Modifier.Count == 0)
            data = await DispatchAsync(context, call);
        else
        {
            // The modifiers wrap the action's own dispatch — the list composes them (outermost first,
            // `on error` clauses written together as one try/catch); then AfterAction fires once per
            // modifier so coverage tracks presence (a modifier wraps, it never runs the standalone path).
            var (execute, wrapError) = await Modifier.Wrap(() => DispatchAsync(context, call), context);
            if (wrapError != null) return context.Error(wrapError);
            data = await execute!();
            foreach (var modifier in Modifier)
                await context.LifecycleFor(modifier).After.Run(
                    context, new app.@event.moment.@this(app.@event.Trigger.AfterAction, modifier, data));
        }

        // %!data% is the last action's result, stored AS-IS. A reference stays a
        // reference and a lazy source stays unread — %!data% never forces a value.
        // Resolution happens only when a real consumer opens the door; storing the
        // value here would read a pending file / resolve a %ref% at every action.
        if (data.Success)
            await context.Variable.Set("!data", data);

        var afterResult = await lifecycle.After.Run(context, new app.@event.moment.@this(app.@event.Trigger.AfterAction, this, data));
        if (!afterResult.Success) return afterResult;

        return data;
    }

    /// <summary>
    /// Dispatches this action inside the frame <see cref="Run"/> pushed for it: resolves the
    /// handler, saves/restores Context anchors, and hands off to the Call, which owns the
    /// exception translation. The frame is NOT created here — a retry dispatches again into
    /// the same frame, and a modifier recovering from a failure is still inside it.
    /// </summary>
    private async Task<global::app.data.@this> DispatchAsync(
        actor.context.@this context, global::app.callstack.call.@this call)
    {
        // Uniform dispatch: always resolve the shell + run Resolve (the seam). A C#-composed
        // Seed (app.Run) rides on the entity and is read by the generated Resolve as the
        // pass-through for its set params — no separate skip-Resolve path.
        var (code, error) = Instance(context);
        if (error != null) return context.Error(error);

        using var _anchor = context.AnchorScope(this);
        return await call.ExecuteAsync(code!, context);
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
    /// PLang name of the action's return type T (when Run() returns Task&lt;Data&lt;T&gt;&gt;).
    /// Null when Run() returns bare <c>Task&lt;Data&gt;</c> — i.e. void: the action has no
    /// meaningful value to write to a variable. Compile.llm uses this to choose the Type
    /// for a trailing <c>variable.set</c> after a <c>write to %x%</c>.
    /// </summary>

}
