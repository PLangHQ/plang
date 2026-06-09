namespace app.module;

/// <summary>
/// The action-handler interface. Every action handler implements IClass via the
/// source generator. Build() is an optional compile-time hook the builder calls
/// during validate (see builder.validate); a handler that doesn't override it
/// contributes nothing (default impl returns Data.Ok()). Build() lets a handler
/// inspect its own arguments at compile time and stamp a Type on the step's
/// terminal variable.set — used for file.read inferring "csv" from a literal
/// path, llm.query inferring "json" from a schema arg, etc.
///
/// Build() returns:
///   - Data.Ok(typeName) — terminal variable.set Type slot becomes typeName.
///   - Data.Ok() (no value) — no terminal Type change; LLM-emitted Type stays.
///   - Data.Fail(err)     — validate aggregates and fails the build.
///
/// Handlers also implement ICodeGenerated (the source-gen contract for
/// ExecuteAsync); IClass is the broader role.
/// </summary>
public interface IClass
{
    System.Threading.Tasks.Task<data.@this> Build()
        => System.Threading.Tasks.Task.FromResult(data.@this.Ok());

    /// <summary>
    /// Prepares the handler for Build() invocation: stamps the action/context and
    /// resolves the action's parameters into the handler's backing fields (async —
    /// %var% decoding goes through the variable store). Source-generator emits the
    /// body on each handler partial; the interface declares it so callers
    /// (builder.validate) can invoke through IClass without reflection.
    /// </summary>
    System.Threading.Tasks.ValueTask SetAction(
        global::app.goal.steps.step.actions.action.@this action,
        global::app.actor.context.@this context);
}
