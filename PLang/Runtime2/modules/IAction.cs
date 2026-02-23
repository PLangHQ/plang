using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using EngineType = PLang.Runtime2.Engine.@this;

namespace PLang.Runtime2.modules;

public interface IAction
{
    EngineType Engine { get; }
    PLangContext Context { get; }
    System.Type? ParameterType { get; }
    void Initialize(EngineType engine, PLangContext context);
    Task<Data> ExecuteAsync(object? parameters);
}
