using PLang.Runtime2.Context;
using PLang.Runtime2.Core;
using PLang.Runtime2.Errors;
using PLang.Runtime2.Memory;

namespace PLang.Runtime2.modules;

public abstract class BaseClass : IClass
{
    public Engine Engine { get; private set; } = null!;
    public PLangContext Context { get; private set; } = null!;
    public virtual System.Type? ParameterType => null;

    protected MemoryStack MemoryStack => Context.MemoryStack;
    protected PLangAppContext AppContext => Context.AppContext;
    protected CancellationToken CancellationToken => Context.CancellationToken;
    protected Interfaces.IPLangFileSystem FileSystem => Engine.FileSystem;

    public void Initialize(Engine engine, PLangContext context)
    {
        Engine = engine;
        Context = context;
    }

    public abstract Task<Data> ExecuteAsync(object? parameters);

    protected static Data Success() => Data.Ok();
    protected static Data Success(object? value) => Data.Ok(value);
    protected static Data Error(string message, string key = "ServiceError", int statusCode = 400)
        => Data.FromError(new ServiceError(message, key, statusCode));
    protected static Data Error(IError error) => Data.FromError(error);
    protected static Task<Data> SuccessTask() => Task.FromResult(Data.Ok());
    protected static Task<Data> SuccessTask(object? value) => Task.FromResult(Data.Ok(value));
    protected static Task<Data> ErrorTask(string message, string key = "ServiceError", int statusCode = 400)
        => Task.FromResult(Data.FromError(new ServiceError(message, key, statusCode)));

    protected static Data<T> Success<T>(T value) => Data<T>.Ok(value);
    protected static Task<Data> SuccessTask<T>(T value) => Task.FromResult<Data>(Data<T>.Ok(value));

    protected Data? GetVariable(string name) => MemoryStack.Get(name);
    protected T? GetVariable<T>(string name) => MemoryStack.Get<T>(name);
    protected void SetVariable(string name, object? value, Memory.Type? type = null)
        => MemoryStack.Set(name, value, type);
}

public abstract class BaseClass<TParams> : BaseClass where TParams : class
{
    public sealed override System.Type? ParameterType => typeof(TParams) == typeof(NullParams) ? null : typeof(TParams);

    public sealed override Task<Data> ExecuteAsync(object? parameters)
    {
        if (typeof(TParams) == typeof(NullParams))
            return ExecuteAsync((TParams)(object)new NullParams());
        if (parameters is not TParams typed)
            return ErrorTask($"Expected {typeof(TParams).Name} parameters");
        return ExecuteAsync(typed);
    }

    protected abstract Task<Data> ExecuteAsync(TParams parameters);
}
