using System.Text.RegularExpressions;
using app;
using app.variable;
using app.@event;
using EventBinding = app.@event.lifecycle.binding.@this;

namespace app.module.action.mock;

[Action("intercept", Cacheable = false)]
public partial class intercept : IContext
{
    public partial data.@this<global::app.type.item.text.@this> Pattern { get; init; }
    public partial data.@this? Return { get; init; }
    public partial data.@this<GoalCall>? Call { get; init; }
    public partial data.@this<global::app.type.item.dict.@this>? Parameter { get; init; }

    public async Task<data.@this<global::app.mock.@this>> Run()
    {
        var handle = new global::app.mock.@this
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Pattern = (await Pattern.Value())!.Clr<string>()!,
            // A spy supplies neither a Return value nor a Call goal — it only
            // observes. An unsupplied optional param is a non-null Uninitialized
            // Data (null model), so "was it supplied?" is IsInitialized, not a
            // C# null check on its absent-citizen value.
            IsSpy = !(Return?.IsInitialized ?? false) && !(Call?.IsInitialized ?? false)
        };

        var returnValue = (Return == null ? null : await Return.Value());
        var goalToCall = (Call == null ? null : await Call.Value()) as global::app.goal.GoalCall;
        var paramMatchers = Parameter == null || await Parameter.IsEmpty() ? null
            : (await Parameter.Value()).Clr<Dictionary<string, object?>>();

        Func<actor.context.@this, app.goal.step.action.@this?, data.@this?, Task<data.@this>> handler = async (context, _, _) =>
        {
            // Find the current action being executed from the step
            var currentAction = FindCurrentAction(context);

            // Check parameter matching if specified
            if (paramMatchers != null && currentAction != null)
            {
                if (!ParametersMatch(currentAction, context.Variable, paramMatchers))
                    return Data(); // no match, let real action run
            }

            // Record the call
            var capturedParams = CaptureParameters(currentAction, context.Variable);
            handle.RecordCall(capturedParams);

            // Goal-based mock — call the goal
            if (goalToCall != null)
                return await context.App!.RunGoalAsync(goalToCall, context, context.CancellationToken);

            // Return value mock — skip action and return the value
            if (returnValue != null)
            {
                context.EventOverride = Data(returnValue);
                return Data(returnValue);
            }

            // Spy mode — just tracked the call, let real action run
            return Data();
        };

        var binding = new EventBinding(
            Trigger.BeforeAction,
            handler,
            actionPattern: (await Pattern.Value())!.Clr<string>()!);

        handle.EventBindingId = binding.Id;

        // Tag binding so mock.reset can find all mock bindings
        binding.Targets.Add(handle);

        Context.Events.Register(binding);

        return Context.Ok<global::app.mock.@this>(handle);
    }

    private static app.goal.step.action.@this? FindCurrentAction(actor.context.@this context)
    {
        var step = context.Step;
        if (step == null) return null;

        // Return the first action (or find the one currently executing)
        // In the BeforeAction event, we're being called for a specific action
        // The event binding was matched by pattern, so all actions in the step match
        foreach (var action in step.Action.Elements)
        {
            return action;
        }
        return null;
    }

    private static Dictionary<string, object?> CaptureParameters(app.goal.step.action.@this? action, global::app.variable.list.@this variables)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (action == null) return result;

        foreach (var param in action.Parameter)
        {
            var value = ResolveParamValue(param, variables);
            result[param.Name] = value;
        }
        return result;
    }

    private static bool ParametersMatch(
        app.goal.step.action.@this action, global::app.variable.list.@this variables, Dictionary<string, object?> matchers)
    {
        foreach (var (name, expected) in matchers)
        {
            var param = action.Parameter.FirstOrDefault(p =>
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
        // A live ref is a stamped template — the stamp gates resolution.
        if (param.Peek() is global::app.type.item.text.@this { Template: not null } st)
            return variables.Resolve(st.Clr<string>()!);

        return param.Peek();
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
