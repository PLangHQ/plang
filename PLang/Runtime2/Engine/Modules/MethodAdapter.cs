using System.Reflection;
using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules;

namespace PLang.Runtime2.Engine.Modules;

/// <summary>
/// Wraps a [Method]-attributed engine method as ICodeGenerated.
/// This lets the existing dispatch (Action.RunAsync → GetCodeGenerated → CodeGeneratedExecuteAsync)
/// work unchanged for [Method] actions.
/// </summary>
internal sealed class MethodAdapter : ICodeGenerated
{
    private readonly object _target;
    private readonly MethodInfo _method;

    public MethodAdapter(object target, MethodInfo method)
    {
        _target = target;
        _method = method;
    }

    public async Task<Data> CodeGeneratedExecuteAsync(
        List<Data> parameters, Engine.@this engine, PLangContext context, List<Data>? defaults = null)
    {
        try
        {
            // Resolve method parameters from the Data list + context
            var methodParams = _method.GetParameters();
            var args = new object?[methodParams.Length];

            for (int i = 0; i < methodParams.Length; i++)
            {
                var mp = methodParams[i];
                var paramType = mp.ParameterType;

                // Inject well-known types
                if (paramType == typeof(Engine.@this))
                {
                    args[i] = engine;
                    continue;
                }
                if (paramType == typeof(PLangContext))
                {
                    args[i] = context;
                    continue;
                }
                if (paramType == typeof(CancellationToken))
                {
                    args[i] = context.CancellationToken;
                    continue;
                }

                // Find matching parameter by name (case-insensitive)
                var data = parameters.FirstOrDefault(d =>
                    string.Equals(d.Name, mp.Name, StringComparison.OrdinalIgnoreCase));
                data ??= defaults?.FirstOrDefault(d =>
                    string.Equals(d.Name, mp.Name, StringComparison.OrdinalIgnoreCase));

                if (data != null)
                {
                    var value = data.Value;

                    // Resolve %var% references
                    if (value is string str && str.Contains('%'))
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(str, @"^%([^%]+)%$");
                        if (match.Success)
                        {
                            var resolved = context.MemoryStack.Get(match.Groups[1].Value);
                            value = resolved?.Value;
                        }
                    }

                    // Convert to target type
                    if (value != null && !paramType.IsAssignableFrom(value.GetType()))
                    {
                        var (converted, _) = Utility.TypeMapping.TryConvertTo(value, paramType);
                        if (converted != null) value = converted;
                    }

                    args[i] = value;
                }
                else if (mp.HasDefaultValue)
                {
                    args[i] = mp.DefaultValue;
                }
                else if (!paramType.IsValueType)
                {
                    args[i] = null;
                }
                else
                {
                    return Data.FromError(new Errors.ServiceError(
                        $"'{mp.Name}' is required", "MissingParameter", 400));
                }
            }

            var result = _method.Invoke(_target, args);

            // Handle async methods
            if (result is Task<Data> taskData)
                return await taskData;
            if (result is Task task)
            {
                await task;
                return Data.Ok();
            }
            if (result is Data data2)
                return data2;

            return Data.Ok(result);
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException ?? ex;
            return Data.FromError(new Errors.ServiceError(
                inner.Message, "MethodError", 500) { Exception = inner });
        }
    }
}
