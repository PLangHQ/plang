using System.Text.RegularExpressions;

namespace app.@event.binding.mock;

/// <summary>
/// A mock — a binding before an action starts (<c>mock.intercept</c>), bound on the module or the catalog action it
/// names (<c>file</c>, <c>file.read</c>): every start of it fires the mock, which records the call and answers in
/// the action's place (a value, or a goal it calls) — or, a spy, lets it run. It is its own record: its calls,
/// <c>callCount</c>, <c>isSpy</c>; <c>mock.verify</c> reads it and <c>mock.reset</c> takes it off.
/// </summary>
[global::app.Attributes.PlangType("mock")]
public sealed class @this : global::app.@event.binding.@this, global::app.type.item.ICreate<@this>
{
    private readonly object? _return;
    private readonly global::app.goal.step.action.@this? _call;
    private readonly Dictionary<string, object?>? _parameters;

    internal @this(global::app.@event.binding.list.@this side, global::app.actor.@this actor, string pattern,
        object? returnValue, global::app.goal.step.action.@this? call, Dictionary<string, object?>? parameters)
        : base(side, actor, global::app.@event.binding.Scope.actor, Always)
    {
        Pattern = pattern;
        _return = returnValue;
        _call = call;
        _parameters = parameters;
    }

    /// <summary>What it mocks: a module (<c>file</c>) or one of its actions (<c>file.read</c>).</summary>
    public string Pattern { get; }

    /// <summary>A spy answers nothing in the action's place — it only records, and the action runs.</summary>
    public bool IsSpy => _return == null && _call == null;

    /// <summary>The calls it recorded.</summary>
    public List<Call> Calls { get; } = new();

    public int CallCount => Calls.Count;

    /// <summary>The action is starting: a call its parameters match is recorded, then answered in the action's
    /// place — the goal it calls, or its value marked handled (which cancels the action) — or, a spy, let run.</summary>
    private protected override async Task<global::app.data.@this> Handle(global::app.type.item.@this item,
        global::app.data.@this result, global::app.actor.context.@this context)
    {
        var action = item as global::app.goal.step.action.@this;
        if (_parameters != null && action != null && !await Matches(action, context)) return result;

        Calls.Add(new Call { Parameters = await Parameters(action, context), Timestamp = DateTime.UtcNow });

        if (_call != null) return await _call.Start(context);
        if (_return == null) return result;
        var answer = context.Ok(_return);
        answer.Handled = true;
        return answer;
    }

    private static async Task<Dictionary<string, object?>> Parameters(global::app.goal.step.action.@this? action,
        global::app.actor.context.@this context)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (action == null) return parameters;
        foreach (var property in action.Property)
            parameters[property.Name] = await Value(property, context);
        return parameters;
    }

    // Whether the action's parameters are the ones this mock takes: each named value, where * stands for any text.
    private async Task<bool> Matches(global::app.goal.step.action.@this action, global::app.actor.context.@this context)
    {
        foreach (var (name, expected) in _parameters!)
        {
            if (action[name] is not { } property) continue;
            if (!Match(expected, await Value(property, context))) return false;
        }
        return true;
    }

    private static async Task<object?> Value(global::app.type.property.@this property, global::app.actor.context.@this context)
    {
        // A live ref is a marked template — it renders itself through its own door.
        if (property.Value is global::app.type.item.text.@this { Template: not null })
            return (await property.Data(context).Value())?.ToString();
        return property.Value;
    }

    // An expected parameter value against the actual one: equal text, where * in the expected stands for any text.
    internal static bool Match(object? expected, object? actual)
    {
        if (expected == null || actual == null) return expected == null && actual == null;
        var pattern = "^" + string.Join(".*", (expected.ToString() ?? "").Split('*').Select(Regex.Escape)) + "$";
        return Regex.IsMatch(actual.ToString() ?? "", pattern, RegexOptions.IgnoreCase);
    }
}

public class Call
{
    public Dictionary<string, object?> Parameters { get; init; } = new();
    public DateTime Timestamp { get; init; }
}
