using PLang.Runtime2.Core;
using PLang.Runtime2.Memory;

namespace PLang.Runtime2.Modules;

/// <summary>
/// The simplest module: set/get variables on MemoryStack.
/// </summary>
public sealed class VariableModule : BaseModule
{
    public override string Name => "variable";

    public override IEnumerable<string> Aliases => new[] { "var", "variables" };

    private static readonly string[] Methods = { "set", "get", "remove", "exists", "clear" };

    public override IEnumerable<string> GetMethods() => Methods;

    public override Task<GoalResult> ExecuteAsync(string method, object? parameters)
    {
        return method.ToLowerInvariant() switch
        {
            "set" => ExecuteSet(parameters),
            "get" => ExecuteGet(parameters),
            "remove" => ExecuteRemove(parameters),
            "exists" => ExecuteExists(parameters),
            "clear" => ExecuteClear(),
            _ => ErrorTask($"Unknown method: {method}", "UnknownMethod", 400)
        };
    }

    private Task<GoalResult> ExecuteSet(object? parameters)
    {
        var request = ParseSetRequest(parameters);
        if (request == null)
            return ErrorTask("Invalid parameters for set operation", "InvalidParameters");

        MemoryStack.Set(request.Name, request.Value, request.Type != null ? TypeInfo.FromName(request.Type) : null);

        return SuccessTask(request.Value);
    }

    private Task<GoalResult> ExecuteGet(object? parameters)
    {
        var name = ParseName(parameters);
        if (string.IsNullOrEmpty(name))
            return ErrorTask("Variable name is required", "MissingName");

        var value = MemoryStack.Get(name);

        return SuccessTask(value?.Value);
    }

    private Task<GoalResult> ExecuteRemove(object? parameters)
    {
        var name = ParseName(parameters);
        if (string.IsNullOrEmpty(name))
            return ErrorTask("Variable name is required", "MissingName");

        var removed = MemoryStack.Remove(name);
        return SuccessTask(removed);
    }

    private Task<GoalResult> ExecuteExists(object? parameters)
    {
        var name = ParseName(parameters);
        if (string.IsNullOrEmpty(name))
            return ErrorTask("Variable name is required", "MissingName");

        var exists = MemoryStack.Contains(name);

        return SuccessTask(exists);
    }

    private Task<GoalResult> ExecuteClear()
    {
        MemoryStack.Clear();
        return SuccessTask();
    }

    private static VariableSetRequest? ParseSetRequest(object? parameters)
    {
        if (parameters == null)
            return null;

        // Handle direct request object
        if (parameters is VariableSetRequest request)
            return request;

        // Handle dictionary-like objects
        if (parameters is IDictionary<string, object?> dict)
        {
            var name = dict.TryGetValue("name", out var n) ? n?.ToString() : null;
            var value = dict.TryGetValue("value", out var v) ? v : null;
            var type = dict.TryGetValue("type", out var t) ? t?.ToString() : null;

            if (string.IsNullOrEmpty(name))
                return null;

            return new VariableSetRequest(name, value, type);
        }

        // Try System.Text.Json element
        if (parameters is System.Text.Json.JsonElement element && element.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            var name = element.TryGetProperty("name", out var n) ? n.GetString() : null;
            object? value = null;
            string? type = null;

            if (element.TryGetProperty("value", out var v))
            {
                value = v.ValueKind switch
                {
                    System.Text.Json.JsonValueKind.String => v.GetString(),
                    System.Text.Json.JsonValueKind.Number => v.TryGetInt64(out var l) ? l : v.GetDouble(),
                    System.Text.Json.JsonValueKind.True => true,
                    System.Text.Json.JsonValueKind.False => false,
                    System.Text.Json.JsonValueKind.Null => null,
                    _ => v.ToString()
                };
            }

            if (element.TryGetProperty("type", out var t))
                type = t.GetString();

            if (string.IsNullOrEmpty(name))
                return null;

            return new VariableSetRequest(name, value, type);
        }

        return null;
    }

    private static string? ParseName(object? parameters)
    {
        if (parameters == null)
            return null;

        // Handle string directly
        if (parameters is string name)
            return name;

        // Handle VariableGetRequest
        if (parameters is VariableGetRequest request)
            return request.Name;

        // Handle dictionary-like objects
        if (parameters is IDictionary<string, object?> dict)
        {
            return dict.TryGetValue("name", out var n) ? n?.ToString() : null;
        }

        // Try System.Text.Json element
        if (parameters is System.Text.Json.JsonElement element)
        {
            if (element.ValueKind == System.Text.Json.JsonValueKind.String)
                return element.GetString();

            if (element.ValueKind == System.Text.Json.JsonValueKind.Object &&
                element.TryGetProperty("name", out var n))
                return n.GetString();
        }

        return null;
    }
}

/// <summary>
/// Request for setting a variable.
/// </summary>
public record VariableSetRequest(string Name, object? Value, string? Type = null);

/// <summary>
/// Request for getting a variable.
/// </summary>
public record VariableGetRequest(string Name);
