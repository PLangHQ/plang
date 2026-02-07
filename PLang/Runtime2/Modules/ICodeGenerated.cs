using PLang.Runtime2.Context;
using PLang.Runtime2.Core;
using PLang.Runtime2.Memory;

namespace PLang.Runtime2.Modules;

/// <summary>
/// Implemented by generated partial handler classes to support lazy parameter resolution.
/// All handlers must implement this interface — Engine requires it (no fallback path).
/// </summary>
public interface ICodeGenerated
{
    Task<Return> CodeGeneratedExecuteAsync(
        List<Data> parameters, Engine engine, PLangContext context);
}
