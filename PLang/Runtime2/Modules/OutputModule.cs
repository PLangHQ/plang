using PLang.Runtime2.Core;

namespace PLang.Runtime2.Modules;

/// <summary>
/// Output module for Runtime2.
/// Writes content to the console.
/// </summary>
public sealed class OutputModule : BaseModule
{
    public override string Name => "output";

    public override IEnumerable<string> Aliases => new[] { "out", "console" };

    public override IEnumerable<string> GetMethods() => new[] { "write" };

    public override Task<GoalResult> ExecuteAsync(string method, object? parameters)
    {
        return method.ToLowerInvariant() switch
        {
            "write" => Write(parameters),
            _ => ErrorTask($"Unknown method: {method}", "UnknownMethod", 400)
        };
    }

    private Task<GoalResult> Write(object? parameters)
    {
        string? content = null;

        if (parameters is string str)
        {
            content = str;
        }
        else if (parameters is IDictionary<string, object?> dict)
        {
            content = dict.TryGetValue("content", out var c) ? c?.ToString() : null;
            if (content == null)
                content = dict.TryGetValue("text", out var t) ? t?.ToString() : null;
            if (content == null)
                content = dict.TryGetValue("message", out var m) ? m?.ToString() : null;
        }
        else if (parameters != null)
        {
            content = parameters.ToString();
        }

        if (content != null)
        {
            Console.WriteLine(content);
        }

        return SuccessTask();
    }
}
