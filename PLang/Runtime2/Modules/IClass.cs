using PLang.Runtime2.Context;
using PLang.Runtime2.Core;

namespace PLang.Runtime2.Modules;

public interface IClass
{
    Engine Engine { get; }
    PLangContext Context { get; }
    Type? ParameterType { get; }
    void Initialize(Engine engine, PLangContext context);
    Task<Return> ExecuteAsync(object? parameters);
}
