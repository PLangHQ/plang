using PLang.Runtime2.Context;
using PLang.Runtime2.Core;
using PLang.Runtime2.Memory;

namespace PLang.Runtime2.actions;

public interface IClass
{
    Engine Engine { get; }
    PLangContext Context { get; }
    System.Type? ParameterType { get; }
    void Initialize(Engine engine, PLangContext context);
    Task<Data> ExecuteAsync(object? parameters);
}
