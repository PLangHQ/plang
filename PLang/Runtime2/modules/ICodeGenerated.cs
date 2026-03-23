using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using EngineType = PLang.Runtime2.Engine.@this;

namespace PLang.Runtime2.modules;

/// <summary>
/// Implemented by generated partial handler classes to support lazy parameter resolution.
/// All handlers must implement this interface — Engine requires it (no fallback path).
/// </summary>
public interface ICodeGenerated
{
    Task<Data> CodeGeneratedExecuteAsync(
        List<Data> parameters, EngineType engine, PLangContext context,
        List<Data>? defaults = null);
}
