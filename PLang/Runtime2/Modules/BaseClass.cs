using PLang.Runtime2.Context;
using PLang.Runtime2.Core;
using PLang.Runtime2.Errors;
using PLang.Runtime2.Memory;

namespace PLang.Runtime2.Modules;

public abstract class BaseClass : IClass
{
    public Engine Engine { get; private set; } = null!;
    public PLangContext Context { get; private set; } = null!;
    public virtual Type? ParameterType => null;

    protected MemoryStack MemoryStack => Context.MemoryStack;
    protected PLangAppContext AppContext => Context.AppContext;
    protected CancellationToken CancellationToken => Context.CancellationToken;
    protected Interfaces.IPLangFileSystem FileSystem => Engine.FileSystem;

    public void Initialize(Engine engine, PLangContext context)
    {
        Engine = engine;
        Context = context;
    }

    public abstract Task<Return> ExecuteAsync(object? parameters);

    protected static Return Success() => new();
    protected static Return Success(object? value) => new() { Value = value };
    protected static Return Error(string message, string key = "ServiceError", int statusCode = 400)
        => new() { Error = new ServiceError(message, key, statusCode) };
    protected static Return Error(IError error) => new() { Error = error };
    protected static Task<Return> SuccessTask() => Task.FromResult(new Return());
    protected static Task<Return> SuccessTask(object? value) => Task.FromResult(new Return { Value = value });
    protected static Task<Return> ErrorTask(string message, string key = "ServiceError", int statusCode = 400)
        => Task.FromResult(new Return { Error = new ServiceError(message, key, statusCode) });

    protected Data? GetVariable(string name) => MemoryStack.Get(name);
    protected T? GetVariable<T>(string name) => MemoryStack.Get<T>(name);
    protected void SetVariable(string name, object? value, TypeInfo? typeInfo = null)
        => MemoryStack.Set(name, value, typeInfo);
}

public abstract class BaseClass<TParams> : BaseClass where TParams : class
{
    public sealed override Type ParameterType => typeof(TParams);

    public sealed override Task<Return> ExecuteAsync(object? parameters)
    {
        return ExecuteAsync(parameters as TParams);
    }

    protected abstract Task<Return> ExecuteAsync(TParams? parameters);
}
