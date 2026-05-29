using System.Text.RegularExpressions;
using app;
using app.variable;
using app.@event;
using EventBinding = app.@event.lifecycle.binding.@this;

namespace app.module.mock;

[Action("intercept", Cacheable = false)]
public partial class intercept : IContext
{
    public partial data.@this<string> Pattern { get; init; }
    public partial data.@this? Return { get; init; }
    public partial data.@this<GoalCall>? Call { get; init; }
    public partial data.@this<Dictionary<string, object?>>? Parameters { get; init; }

    public Task<data.@this<global::app.mock.Mock.@this>> Run()
    {
        var handle = new global::app.mock.Mock.@this
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Pattern = Pattern.Value!,
            IsSpy = Return?.Value == null && Call?.Value == null
        };

        var returnValue = Return?.Value;
        var goalToCall = Call?.Value;
        var paramMatchers = Parameters?.Value;

        Func<actor.context.@this, app.goal.steps.step.actions.action.@this?, data.@this?, Task<data.@this>> handler = async (ctx, _, _) =>
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
            actionPattern: Pattern.Value!);

        handle.EventBindingId = binding.Id;

        // Tag binding so mock.reset can find all mock bindings
        binding.Targets.Add(handle);

        Context.Events.Register(binding);

        return Task.FromResult(data.@this<global::app.mock.Mock.@this>.Ok(handle));
    }

    private static app.goal.steps.step.actions.action.@this? FindCurrentAction(actor.context.@this ctx)
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

    private static Dictionary<string, object?> CaptureParameters(app.goal.steps.step.actions.action.@this? action, global::app.variable.list.@this variables)
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
        app.goal.steps.step.actions.action.@this action, global::app.variable.list.@this variables, Dictionary<string, object?> matchers)
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

    private static object? ResolveParamValue(data.@this param, global::app.variable.list.@this variables)
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
