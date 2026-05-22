using System.Text.Json.Serialization;
using app.variables;
namespace app.goals.goal.steps.step.actions.action;

/// <summary>
/// A single action within a step — the LLM-mapped unit of execution.
/// Identifies the module and handler to invoke, with typed parameters, return mappings, and defaults.
/// </summary>
public sealed partial class @this : modules.IDataWrappable
{
    /// <summary>
    /// OBP: Action is responsible for its own Data representation.
    /// Returns a cached per-execution Data&lt;Action&gt; wrapper from the context.
    /// </summary>
    public global::app.data.@this AsData(actor.context.@this context)
    {
        return context.GetOrCreate(this, () =>
        {
            var data = new global::app.data.@this<@this>("", this);
            data.Context = context;
            return data;
        });
    }

    [JsonIgnore]
    public System.Type? ParameterSchema { get; init; }
    [Store, LlmBuilder, Debug, Default]
    [JsonPropertyName("module")]
    [Newtonsoft.Json.JsonProperty("module")]
    public string Module { get; set; } = "";

    [Store, LlmBuilder, Debug, Default]
    [JsonPropertyName("action")]
    [Newtonsoft.Json.JsonProperty("action")]
    public string ActionName { get; set; } = "";

    [Store, LlmBuilder, Debug, Default]
    public List<global::app.data.@this> Parameters { get; init; } = new();

    [Store, Debug, Default]
    public List<global::app.data.@this>? Defaults { get; set; }

    [Store, Debug, Default]
    public modifiers.@this Modifiers { get; init; } = new();

    [Debug]
    public List<Info> Errors { get; init; } = new();

    [Debug]
    public List<Info> Warnings { get; init; } = new();

    [JsonIgnore]
    public bool Cacheable { get; init; } = true;

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
    public modules.ICodeGenerated? PreboundHandler { get; init; }

    /// <summary>
    /// True for any condition chain action: condition.if, condition.elseif, or condition.else.
    /// Used by SplitAtConditions / ComputeBranchChain to split an orchestrated step's actions
    /// into per-branch groups.
    /// </summary>
    [JsonIgnore]
    public bool IsCondition =>
        string.Equals(Module, "condition", StringComparison.OrdinalIgnoreCase) &&
        (string.Equals(ActionName, "if", StringComparison.OrdinalIgnoreCase)
      || string.Equals(ActionName, "elseif", StringComparison.OrdinalIgnoreCase)
      || string.Equals(ActionName, "else", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// True only for the head of a condition chain (condition.if). Coverage records sites
    /// against the head — elseif/else participate in the chain but don't own the site.
    /// </summary>
    [JsonIgnore]
    public bool IsIfHead =>
        string.Equals(Module, "condition", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(ActionName, "if", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when this is the first condition.if action in its step. Used by coverage
    /// to ignore inner-elseif simple-path firings that would otherwise mix
    /// true/false labels into the orchestrator's declared chain.
    /// </summary>
    [JsonIgnore]
    public bool IsFirstConditionInStep => Step?.Actions.IsFirstCondition(this) ?? false;

    [JsonIgnore]
    public Step? Step { get; set; }

    private modules.Events? _events;
    [JsonIgnore]
    public modules.Events Events
    {
        get => _events ??= new modules.Events(this);
    }

    public List<global::app.data.@this> Examples { get; init; } = new();

    /// <summary>
    /// One-sentence description of what this action does, sourced from
    /// [System.ComponentModel.Description] on the action class.
    /// Populated by Modules.Describe(); null when no description attribute is present.
    /// </summary>
    [JsonIgnore]
    public string? Description { get; init; }

    /// <summary>
    /// One-sentence description of the module this action belongs to, sourced from
    /// [ModuleDescription] on the first action class in the module namespace.
    /// Populated by Modules.Describe(); null when no attribute is present.
    /// </summary>
    [JsonIgnore]
    public string? ModuleDescription { get; init; }

    /// <summary>
    /// True when this action is declared as a modifier via [Modifier] on its class.
    /// Modifier actions wrap the preceding action rather than standing on their own
    /// (e.g. cache.wrap, error.handle, timeout.after). The catalog renders modifier
    /// actions in their own section. Per-action, so a module can carry a mix —
    /// e.g. error.throw (action) and error.handle (modifier) share the error module.
    /// </summary>
    [JsonIgnore]
    public bool IsModifier { get; init; }

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
        return data ?? global::app.data.@this.NotFound(name);
    }

    /// <summary>
    /// Runs this action: lifecycle events → dispatch → return mapping.
    /// Context travels as parameter — actions are shared objects, not per-request.
    /// Owns its own callstack push/pop, anchor save/restore, exception translation
    /// (formerly App.Run's body — collapsed in stage 2a.5 since "action owns its
    /// execution").
    /// </summary>
    public async Task<global::app.data.@this> RunAsync(actor.context.@this context)
    {
        var lifecycle = context.LifecycleFor(this);

        var beforeResult = await lifecycle.Before.Run(context, app.events.EventType.BeforeAction);
        if (!beforeResult.Success) return beforeResult;

        global::app.data.@this result;
        if (beforeResult.Handled)
        {
            // Override path: the BeforeAction binding supplied this action's result
            // (mock.intercept, event.skipAction). Clear Handled so the outer step
            // loop doesn't misread "dispatch was short-circuited" as "stop the step" —
            // the next action in the chain still needs to run on this result.
            result = beforeResult;
            result.Handled = false;
        }
        else
        {
            Func<Task<global::app.data.@this>> dispatch = () => DispatchAsync(context);
            result = await Modifiers.RunAsync(dispatch, context);
        }

        if (result.Success)
        {
            // Alias the result as %!data% — Variables.Set stores Data references as-is
            // (no clone, no rename). Same object is reachable via both %!data% and
            // whatever name the producing handler owns (e.g., variable.set's stored entry).
            // Override path (beforeResult.Handled) flows through the same write so
            // mocks and event.skipAction feed the next action like any real result.
            context.Variables.Set("!data", result);
        }

        var afterResult = await lifecycle.After.Run(context, app.events.EventType.AfterAction, this, result);
        if (!afterResult.Success) return afterResult;

        return result;
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
        modules.ICodeGenerated? handler;
        global::app.errors.IError? error;
        if (PreboundHandler != null)
        {
            handler = PreboundHandler;
            error = null;
        }
        else
        {
            (handler, error) = app.Modules.GetCodeGenerated(this);
            if (error != null) return global::app.data.@this.FromError(error);
        }

        // CallStackOverflowException (depth limit or ContainsGoal cycle) trips at Push,
        // before the call frame is on the stack — catch it here so the contract
        // (returns Data, never throws) holds. Once Push succeeds the Call owns its
        // own try/catch via ExecuteAsync.
        global::app.callstack.call.@this call;
        try { call = app.CallStack.Push(this, context.Variables); }
        catch (global::app.errors.CallStackOverflowException ex)
        {
            var caller = app.CallStack.Current;
            var chain = caller != null ? caller.SnapshotChain() : Array.Empty<global::app.callstack.call.@this>();
            var overflowErr = new global::app.errors.ServiceError(ex.Message, this.Step!, chain, "CallStackOverflow", 500) { Exception = ex };
            app.CallStack.Audit.Add(overflowErr);
            return global::app.data.@this.FromError(overflowErr);
        }

        // Dispose order matters: anchor restore must run BEFORE Call's await-using
        // dispose (AsyncLocal restore, Children removal, Variables.OnSet unsubscribe).
        // C# disposes in reverse declaration order — declare `await using call` first
        // so the inner `using anchor` disposes first.
        await using var _ = call;
        using var _anchor = context.AnchorScope(this);
        // PreboundHandler path: handler properties are already set by inline C#
        // composition. Pass `null` to ExecuteAsync so the generated reset/resolve
        // loop is skipped — matches the former App.RunAction behaviour.
        if (PreboundHandler != null)
            return await handler!.ExecuteAsync(null!, context);
        return await call.ExecuteAsync(handler!, context);
    }

    /// <summary>
    /// Wraps the given inner delegate with this modifier action. Resolves this action's
    /// handler, verifies it implements IModifier, and runs ExecuteAsync so the source-generated
    /// properties are populated before Wrap() reads them. Called by Modifiers.RunAsync.
    /// </summary>
    public async Task<(Func<Task<global::app.data.@this>>? Wrapped, global::app.errors.IError? Error)> WrapAround(
        Func<Task<global::app.data.@this>> next,
        actor.context.@this context)
    {
        var (handler, error) = context.App!.Modules.GetCodeGenerated(this);
        if (error != null) return (null, error);
        if (handler is not modules.IModifier mod)
        {
            // Pinpoint WHERE the misplaced "modifier" lives. Modifier Actions don't
            // have their own Step propagated from the host (the Actions container only
            // sets Step on top-level items), so fall back to the live runtime context
            // for goal/step info.
            var step = Step ?? context.Step;
            var goalName = step?.Goal?.Name;
            var goalPath = step?.Goal?.Path;
            var stepText = step?.Text;
            var stepIndex = step?.Index;
            var loc = (goalName, goalPath, stepText, stepIndex) switch
            {
                ({ } g, { } p, { } t, { } i) => $" — in goal {g} ({p}) step [{i}] \"{t}\"",
                ({ } g, _, { } t, { } i) => $" — in goal {g} step [{i}] \"{t}\"",
                (_, _, { } t, { } i) => $" — in step [{i}] \"{t}\"",
                _ => ""
            };
            return (null, new global::app.errors.ActionError(
                $"{Module}.{ActionName} is not a modifier (it was placed in a modifiers array but isn't one). " +
                $"Move it out as a peer action in the step's top-level actions array.{loc}",
                "ModifierError", 400));
        }

        await handler.ExecuteAsync(this, context);
        return (mod.Wrap(next, context), null);
    }

    /// <summary>
    /// Return type properties for the builder summary. Null when Run() returns plain Data.
    /// Derived from the concrete return type of Run() via reflection in Describe().
    /// </summary>
    public List<global::app.data.@this>? ReturnType { get; init; }
}
