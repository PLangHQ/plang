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

    [Store, LlmBuilder, Debug, Default]
    [JsonPropertyName("module")]
    [Newtonsoft.Json.JsonProperty("module")]
    public string Module { get; set; } = "";

    [Store, LlmBuilder, Debug, Default]
    [JsonPropertyName("action")]
    [Newtonsoft.Json.JsonProperty("action")]
    public string ActionName { get; set; } = "";

    /// <summary>The qualified action name — "file.read". The class-zoom face templates read
    /// as one token instead of composing module + action.</summary>
    [JsonIgnore]
    public string Name => $"{Module}.{ActionName}";

    [Store, LlmBuilder, Debug, Default]
    public List<global::app.data.@this> Parameters { get; init; } = new();

    [Store, Debug, Default]
    public List<global::app.data.@this>? Defaults { get; set; }

    /// <summary>The modifiers wrapping this action (cache.wrap, error.handle, timeout.after) — an
    /// internal typed list; the action owns their right-to-left wrap fold (see RunAsync) and their
    /// sort (actions.Nest). Position within the slot carries the nesting order at runtime.</summary>
    [Store, Debug, Default]
    public List<modifier.@this> Modifiers { get; init; } = new();

    /// <summary>The branch body of a control-flow action (the steps that run when this condition fires).
    /// Empty on every non-control-flow action — the fire gate is <c>Child.Count &gt; 0 &amp;&amp; truthy</c>.
    /// Both nesting forms land here: inline <c>if/elseif/else</c> (each condition action carries its body)
    /// and indented sub-step blocks (folded onto the gate action). A <c>step.list</c>, so it runs itself.</summary>
    [Store, Debug, Default]
    public global::app.goal.step.list.@this Child { get; init; } = new(new List<global::app.goal.step.@this>());

    [Debug]
    public List<Info> Errors { get; init; } = new();

    [Debug]
    public List<Info> Warnings { get; init; } = new();

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
        string.Equals(Module, "condition", StringComparison.OrdinalIgnoreCase) &&
        (string.Equals(ActionName, "if", StringComparison.OrdinalIgnoreCase)
      || string.Equals(ActionName, "elseif", StringComparison.OrdinalIgnoreCase)
      || string.Equals(ActionName, "else", StringComparison.OrdinalIgnoreCase));

    [JsonIgnore]
    public Step? Step { get; set; }

    private module.Events? _events;
    [JsonIgnore]
    public module.Events Events
    {
        get => _events ??= new module.Events(this);
    }

    // Teaching prose (Description / Notes / Examples) is no longer stored on the action host — it lives
    // as lazy `file` handles on the class-zoom partial (this.Schema.cs), over
    // os/system/modules/{Module}/{ActionName}.{facet}.md. Templates read module-first + action through
    // those doors; the old string fields + MergeLayers `*Rendered` cousins are retired.


    /// <summary>
    /// Looks up a parameter by name. Walks Parameters first, falls back to Defaults,
    /// returns Data.NotFound when missing. Pure lookup — no resolution side effects.
    /// Resolution happens later via Data.As&lt;T&gt;(context). Context is part of the
    /// contract for symmetry with As&lt;T&gt;(context); kept as a hook even though
    /// today's lookup is context-free.
    /// </summary>
    public global::app.data.@this GetParameter(string name, actor.context.@this context)
    {
        var data = Parameters?.FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (data != null) return data;
        data = Defaults?.FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        return data ?? context.NotFound(name);
    }

    /// <summary>
    /// Runs this action: lifecycle events → dispatch → return mapping.
    /// Context travels as parameter — actions are shared objects, not per-request.
    /// Owns its own callstack push/pop, anchor save/restore, exception translation
    /// (formerly App.Run's body — collapsed in stage 2a.5 since "action owns its
    /// execution").
    /// </summary>
    public async Task<global::app.data.@this> Run(actor.context.@this context)
    {
        var lifecycle = context.LifecycleFor(this);

        var beforeResult = await lifecycle.Before.Run(context, app.@event.Trigger.BeforeAction);
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
        else if (Modifiers.Count == 0)
            data = await DispatchAsync(context);
        else
        {
            // The action composes its modifiers around its own dispatch — right-to-left, lowest
            // Position outermost (the slot is pre-sorted by Nest). Each modifier wraps the inner in
            // ITSELF (modifier.Wrap); then AfterAction fires once per modifier so coverage tracks
            // presence (a modifier wraps, it never runs the standalone path).
            Func<Task<global::app.data.@this>> execute = () => DispatchAsync(context);
            for (int i = Modifiers.Count - 1; i >= 0; i--)
            {
                var (wrapped, wrapError) = await Modifiers[i].Wrap(execute, context);
                if (wrapError != null) return context.Error(wrapError);
                execute = wrapped!;
            }
            data = await execute();
            foreach (var modifier in Modifiers)
                await context.LifecycleFor(modifier).After.Run(
                    context, app.@event.Trigger.AfterAction, modifier, data);
        }

        // %!data% is the last action's result, stored AS-IS. A reference stays a
        // reference and a lazy source stays unread — %!data% never forces a value.
        // Resolution happens only when a real consumer opens the door; storing the
        // value here would read a pending file / resolve a %ref% at every action.
        if (data.Success)
            await context.Variable.Set("!data", data);

        var afterResult = await lifecycle.After.Run(context, app.@event.Trigger.AfterAction, this, data);
        if (!afterResult.Success) return afterResult;

        return data;
    }

    /// <summary>
    /// Dispatches this action through the production execution path: pushes a Call
    /// onto the CallStack, saves/restores Context anchors, translates CLR exceptions
    /// into ServiceError. Absorbed from the former <c>App.Run</c> in stage 2a.5 so
    /// the action owns its own execution.
    /// </summary>
    private async Task<global::app.data.@this> DispatchAsync(actor.context.@this context)
    {
        var app = context.App!;
        // Uniform dispatch: always resolve the shell + run Resolve (the seam). A C#-composed
        // Seed (app.Run) rides on the entity and is read by the generated Resolve as the
        // pass-through for its set params — no separate skip-Resolve path.
        var (handler, error) = app.Module.GetCodeGenerated(this, context);
        if (error != null) return context.Error(error);

        // CallStackOverflowException (depth limit or ContainsGoal cycle) trips at Push,
        // before the call frame is on the stack — catch it here so the contract
        // (returns Data, never throws) holds. Once Push succeeds the Call owns its
        // own try/catch via ExecuteAsync.
        global::app.callstack.call.@this call;
        try { call = context.CallStack.Push(this, context.Variable); }
        catch (global::app.error.CallStackOverflowException ex)
        {
            var caller = context.CallStack.Current;
            var chain = caller != null ? caller.SnapshotChain() : Array.Empty<global::app.callstack.call.@this>();
            var overflowErr = new global::app.error.ServiceError(ex.Message, this.Step!, chain, "CallStackOverflow", 500) { Exception = ex };
            context.CallStack.Audit.Add(overflowErr);
            return context.Error(overflowErr);
        }

        // Dispose order matters: anchor restore must run BEFORE Call's await-using
        // dispose (AsyncLocal restore, Children removal, Variables.OnSet unsubscribe).
        // C# disposes in reverse declaration order — declare `await using call` first
        // so the inner `using anchor` disposes first.
        await using var _ = call;
        using var _anchor = context.AnchorScope(this);
        return await call.ExecuteAsync(handler!, context);
    }

    /// <summary>
    /// Return type properties for the builder summary. Null when Run() returns plain Data.
    /// Derived from the concrete return type of Run() via reflection in Describe().
    /// </summary>
    public List<global::app.data.@this>? ReturnType { get; init; }

    /// <summary>
    /// PLang name of the action's return type T (when Run() returns Task&lt;Data&lt;T&gt;&gt;).
    /// Null when Run() returns bare <c>Task&lt;Data&gt;</c> — i.e. void: the action has no
    /// meaningful value to write to a variable. Compile.llm uses this to choose the Type
    /// for a trailing <c>variable.set</c> after a <c>write to %x%</c>.
    /// </summary>
    public string? ReturnTypeName { get; init; }
}
