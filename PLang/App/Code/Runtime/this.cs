using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using App.Errors;
using AppData = App.Data.@this;

namespace App.Code.Runtime;

/// <summary>
/// Wraps an assembly loaded into a collectible ALC and exposes
/// <c>Start(ctx)</c> / <c>Invoke(name, args, ctx)</c> for the entry type.
/// Owns its ALC — disposal unloads.
/// </summary>
public sealed class @this : IAsyncDisposable
{
    private static readonly ConcurrentDictionary<System.Type, MethodInfo> AsCache = new();

    private readonly AssemblyLoadContext _alc;
    private readonly System.Type _entry;
    private bool _disposed;

    public @this(AssemblyLoadContext alc, Assembly assembly)
    {
        _alc = alc;
        _entry = assembly.GetTypes().FirstOrDefault(t => t.IsPublic && t.IsClass && !t.IsAbstract)
            ?? throw new InvalidOperationException(
                $"No public class found in assembly '{assembly.GetName().Name}'");
    }

    public string EntryTypeName => _entry.FullName ?? _entry.Name;

    /// <summary>
    /// Invokes <c>Start(Data data)</c> on the entry class. Single-slot contract:
    /// the script always sees one <see cref="Data.@this"/>. If the script's
    /// <c>Start</c> takes zero args (no input), <paramref name="data"/> is
    /// dropped and the empty-arg call goes through.
    /// </summary>
    public Task<AppData> Start(AppData data, Actor.Context.@this context)
    {
        var entryStart = _entry.GetMethod("Start", BindingFlags.Public | BindingFlags.Instance);
        var args = entryStart != null && entryStart.GetParameters().Length == 0
            ? new List<AppData>()
            : new List<AppData> { data };
        return Invoke("Start", args, context);
    }

    public async Task<AppData> Invoke(
        string methodName,
        IReadOnlyList<AppData> args,
        Actor.Context.@this context)
    {
        var mi = _entry.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        if (mi is null)
            return AppData.FromError(new ActionError(
                $"Method '{methodName}' not found on {_entry.Name}",
                "MethodNotFound", 404));

        var parameters = mi.GetParameters();
        if (parameters.Length != args.Count)
            return AppData.FromError(new ActionError(
                $"Method '{methodName}' expects {parameters.Length} argument(s), got {args.Count}",
                "ArityMismatch", 400));

        var bound = new object?[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            var paramType = parameters[i].ParameterType;
            var asMethod = AsCache.GetOrAdd(paramType, static t =>
                typeof(AppData).GetMethod(nameof(AppData.As))!.MakeGenericMethod(t));
            var typed = (AppData)asMethod.Invoke(args[i], new object?[] { context })!;
            if (!typed.Success) return typed;
            bound[i] = typed.Value;
        }

        var instance = CreateInstance(context);
        if (instance is null)
            return AppData.FromError(new ActionError(
                $"Class '{_entry.Name}' must have a () or (Context) constructor",
                "UnsupportedConstructor", 400));

        Task task;
        try
        {
            task = (Task)mi.Invoke(instance, bound)!;
        }
        catch (TargetInvocationException tex)
        {
            return AppData.FromError(ActionError.FromException(
                tex.InnerException ?? tex, "InvocationFailed", 500));
        }

        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return AppData.FromError(ActionError.FromException(ex, "InvocationFailed", 500));
        }

        // Use the declared return type — async state machines run as Task<VoidTaskResult>
        // even for non-generic Task, so task.GetType() is the wrong source of truth.
        var ret = mi.ReturnType;
        if (ret.IsGenericType && ret.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var result = ret.GetProperty("Result")!.GetValue(task);
            return AppData.Ok(result);
        }
        return AppData.Ok();
    }

    private object? CreateInstance(Actor.Context.@this context)
    {
        var ctxCtor = _entry.GetConstructor(new[] { typeof(Actor.Context.@this) });
        if (ctxCtor != null) return ctxCtor.Invoke(new object[] { context });

        var emptyCtor = _entry.GetConstructor(System.Type.EmptyTypes);
        if (emptyCtor != null) return emptyCtor.Invoke(null);

        return null;
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        _alc.Unload();
        return ValueTask.CompletedTask;
    }
}
