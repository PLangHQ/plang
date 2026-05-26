namespace app.modules;

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
}
