using System.Text.RegularExpressions;
using App;
using App.Variables;
using App.Events;
using EventBinding = App.Events.Lifecycle.Bindings.Binding.@this;

namespace App.modules.mock;

[Action("intercept", Cacheable = false)]
public partial class MockAction : IContext
{
    public partial Data.@this<string> ActionPattern { get; init; }
    public partial Data.@this? ReturnValue { get; init; }
    public partial Data.@this<GoalCall>? GoalToCall { get; init; }
    public partial Data.@this<Dictionary<string, object?>>? Parameters { get; init; }

    public Task<Data.@this> Run()
    {
        var handle = new types.MockHandle
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            ActionPattern = ActionPattern.Value!,
            IsSpy = ReturnValue?.Value == null && GoalToCall?.Value == null
        };

        var returnValue = ReturnValue?.Value;
        var goalToCall = GoalToCall?.Value;
        var paramMatchers = Parameters?.Value;

        Func<Actor.Context.@this, Goals.Goal.Steps.Step.Actions.Action.@this?, Data.@this?, Task<Data.@this>> handler = async (ctx, _, _) =>
        {
            // Find the current action being executed from the step
            var currentAction = FindCurrentAction(ctx);

            // Check parameter matching if specified
            if (paramMatchers != null && currentAction != null)
            {
                if (!ParametersMatch(currentAction, ctx.Variables, paramMatchers))
                    return Data(); // no match, let real action run
            }

            // Record the call
            var capturedParams = CaptureParameters(currentAction, ctx.Variables);
            handle.RecordCall(capturedParams);

            // Goal-based mock — call the goal
            if (goalToCall != null)
                return await ctx.App!.RunGoalAsync(goalToCall, ctx, ctx.CancellationToken);

            // Return value mock — skip action and return the value
            if (returnValue != null)
            {
                ctx.EventOverride = Data(returnValue);
                return Data(returnValue);
            }

            // Spy mode — just tracked the call, let real action run
            return Data();
        };

        var binding = new EventBinding(
            EventType.BeforeAction,
            handler,
            actionPattern: ActionPattern.Value!);

        handle.EventBindingId = binding.Id;

        // Tag binding so mock.reset can find all mock bindings
        binding.Targets.Add(handle);

        Context.Events.Register(binding);

        return Task.FromResult(Data(handle));
    }

    private static Goals.Goal.Steps.Step.Actions.Action.@this? FindCurrentAction(Actor.Context.@this ctx)
    {
        var step = ctx.Step;
        if (step == null) return null;

        // Return the first action (or find the one currently executing)
        // In the BeforeAction event, we're being called for a specific action
        // The event binding was matched by pattern, so all actions in the step match
        foreach (var action in step.Actions)
        {
            return action;
        }
        return null;
    }

    private static Dictionary<string, object?> CaptureParameters(Goals.Goal.Steps.Step.Actions.Action.@this? action, Variables.@this variables)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (action == null) return result;

        foreach (var param in action.Parameters)
        {
            var value = ResolveParamValue(param, variables);
            result[param.Name] = value;
        }
        return result;
    }

    private static bool ParametersMatch(
        Goals.Goal.Steps.Step.Actions.Action.@this action, Variables.@this variables, Dictionary<string, object?> matchers)
    {
        foreach (var (name, expected) in matchers)
        {
            var param = action.Parameters.Find(p =>
                p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (param == null) continue;

            var actual = ResolveParamValue(param, variables);
            if (!MatchValue(expected, actual))
                return false;
        }
        return true;
    }

    private static object? ResolveParamValue(Data.@this param, Variables.@this variables)
    {
        if (param.Value is string s && s.Contains('%'))
            return variables.Resolve(s);

        return param.Value;
    }

    /// <summary>
    /// Converts a PLang pattern to a regex pattern.
    /// - Standalone `*` (not preceded by `.` or `\`) becomes `.*`
    /// - If pattern has regex-specific chars, used as-is
    /// - Plain string is escaped for exact match
    /// </summary>
    public static string ToRegex(string pattern)
    {
        bool hasWildcard = false;
        for (int i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] == '*' && (i == 0 || (pattern[i - 1] != '.' && pattern[i - 1] != '\\')))
            {
                hasWildcard = true;
                break;
            }
        }

        if (hasWildcard)
        {
            var segments = new List<string>();
            int start = 0;
            for (int i = 0; i < pattern.Length; i++)
            {
                if (pattern[i] == '*' && (i == 0 || (pattern[i - 1] != '.' && pattern[i - 1] != '\\')))
                {
                    segments.Add(Regex.Escape(pattern[start..i]));
                    segments.Add(".*");
                    start = i + 1;
                }
            }
            segments.Add(Regex.Escape(pattern[start..]));
            return "^" + string.Join("", segments) + "$";
        }

        // Check if it looks like an intentional regex (has regex metacharacters)
        bool looksLikeRegex = Regex.IsMatch(pattern, @"[\\^$+?\[\]{}()|]");
        if (looksLikeRegex)
            return "^" + pattern + "$";

        // Plain string — exact match
        return "^" + Regex.Escape(pattern) + "$";
    }

    public static bool MatchValue(object? pattern, object? actual)
    {
        if (pattern == null && actual == null) return true;
        if (pattern == null || actual == null) return false;

        var patternStr = pattern.ToString() ?? "";
        var actualStr = actual.ToString() ?? "";

        var regex = ToRegex(patternStr);
        return Regex.IsMatch(actualStr, regex, RegexOptions.IgnoreCase);
    }
}
