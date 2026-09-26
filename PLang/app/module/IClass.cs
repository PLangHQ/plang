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
/// Handlers also implement ICodeGenerated (the source-gen contract for Resolve /
/// Attach / Execute); IClass is the broader role. To invoke Build(), resolve a
/// populated instance via <c>ICodeGenerated.Resolve</c>, then call Build() on it.
/// </summary>
public interface IClass
{
    System.Threading.Tasks.Task<data.@this> Build()
        => System.Threading.Tasks.Task.FromResult(data.@this.Ok());

    /// <summary>Build-time judgement of the handler's own properties — the parameter COMBINATIONS
    /// only the handler knows are legal. Runs on the bound handler (typed views, unresolved);
    /// a %var% is unknown at build, so read with Peek and open .Value() only when the check truly
    /// needs it. Null when nothing is wrong — the default.</summary>
    System.Threading.Tasks.Task<global::app.error.Error?> Validate()
        => System.Threading.Tasks.Task.FromResult<global::app.error.Error?>(null);

    /// <summary>Build-time verdict on the handler's LITERAL parameters — each opened through its own
    /// typed view, the door the run opens. A template, a variable, an absent slot and a slot holding
    /// a domain object are skipped; nothing resolves, nothing reads a resource. One error per literal
    /// its slot declines, carrying the type's own reason. Generated per handler; empty by default.</summary>
    System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<global::app.error.Error>> Parse()
        => System.Threading.Tasks.Task.FromResult<System.Collections.Generic.IReadOnlyList<global::app.error.Error>>(
            System.Array.Empty<global::app.error.Error>());

    /// <summary>Build-time verdict on the handler's VARIABLE parameters against what its context's store
    /// knows — the scratch store the build walks the goal into. A slot holding a whole plain %name% the
    /// store holds a value for is opened through its own typed view, the door the run opens; one error per
    /// slot its type declines. A variable the store doesn't know, a navigation, a text with variables
    /// inside — unknown — pass. Generated per handler; empty by default.</summary>
    System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<global::app.error.Error>> Check()
        => System.Threading.Tasks.Task.FromResult<System.Collections.Generic.IReadOnlyList<global::app.error.Error>>(
            System.Array.Empty<global::app.error.Error>());

    /// <summary>The goal this handler calls, as its properties name it now — selected the way its run
    /// selects it, on the bound handler (typed views, unresolved). A %var% name is only known at run,
    /// so it answers none. None — the default — for a handler that calls no goal.</summary>
    System.Threading.Tasks.Task<global::app.goal.@this?> Callee()
        => System.Threading.Tasks.Task.FromResult<global::app.goal.@this?>(null);
}
