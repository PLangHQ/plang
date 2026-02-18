using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using EngineType = PLang.Runtime2.Engine.@this;

namespace PLang.Runtime2.actions;

public interface IClass
{
    EngineType Engine { get; }
    PLangContext Context { get; }
    System.Type? ParameterType { get; }
    void Initialize(EngineType engine, PLangContext context);
    Task<Data> ExecuteAsync(object? parameters);
}
