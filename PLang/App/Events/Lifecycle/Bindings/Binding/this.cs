using System.Text.RegularExpressions;
using App.Context;
using App.Events;
using App.Variables;

namespace App.Events.Lifecycle.Bindings.Binding;

/// <summary>
/// Represents an event binding in App.
/// </summary>
public sealed class @this
{
    public string Id { get; }
    public EventType Type { get; }
    public string? GoalNamePattern { get; }
    public string? StepPattern { get; }
    public string? ActionPattern { get; }
    public Func<Context.@this, Task<Data>> Handler { get; }
    public Goals.Goal.GoalCall? GoalToCall { get; }
    public int Priority { get; }
    public bool StopOnError { get; }
    public bool IsRegex { get; }

    public List<object> Targets { get; } = new();

    /// <summary>
    /// Runs this binding's handler, skipping if already executing (re-entry guard).
    /// </summary>
    public async Task<Data> Run(Context.@this context)
    {
        if (!context.TryEnterEvent(Id))
            return Data.Ok();

        var result = await Handler(context);
        context.ExitEvent(Id);

        // Check if handler set an override via event.skipAction.
        // Only consume the override for action-level events (BeforeAction/AfterAction)
        // to prevent step/goal-level events from accidentally eating the override.
        if (Type == EventType.BeforeAction || Type == EventType.AfterAction)
        {
            var @override = context.EventOverride;
            if (@override != null)
            {
                context.EventOverride = null;
                @override.Handled = true;
                return @override;
            }
        }

        if (!result.Success && !StopOnError)
            return Data.Ok();

        return result;
    }

    public @this(
        EventType type,
        Func<Context.@this, Task<Data>> handler,
        string? goalNamePattern = null,
        string? stepPattern = null,
        string? actionPattern = null,
        int priority = 0,
        bool stopOnError = true,
        bool isRegex = false,
        Goals.Goal.GoalCall? goalToCall = null)
    {
        Id = Guid.NewGuid().ToString("N")[..8];
        Type = type;
        GoalNamePattern = goalNamePattern;
        StepPattern = stepPattern;
        ActionPattern = actionPattern;
        Handler = handler;
        Priority = priority;
        StopOnError = stopOnError;
        IsRegex = isRegex;
        GoalToCall = goalToCall;
    }

    /// <summary>
    /// Checks if this binding matches the given goal name.
    /// </summary>
    public bool MatchesGoal(string goalName)
    {
        if (string.IsNullOrEmpty(GoalNamePattern))
            return true;

        if (GoalNamePattern == "*")
            return true;

        if (IsRegex)
            return Regex.IsMatch(goalName, GoalNamePattern, RegexOptions.IgnoreCase);

        if (GoalNamePattern.EndsWith("*"))
        {
            var prefix = GoalNamePattern[..^1];
            return goalName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(goalName, GoalNamePattern, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if this binding matches the given step text.
    /// </summary>
    public bool MatchesStep(string stepText)
    {
        if (string.IsNullOrEmpty(StepPattern))
            return true;

        if (StepPattern == "*")
            return true;

        if (IsRegex)
            return Regex.IsMatch(stepText, StepPattern, RegexOptions.IgnoreCase);

        return stepText.Contains(StepPattern, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if this binding matches the given module and action name.
    /// Supports exact match ("variable.set") and wildcard ("variable.*").
    /// </summary>
    public bool MatchesAction(string module, string actionName)
    {
        if (string.IsNullOrEmpty(ActionPattern))
            return true;

        if (ActionPattern == "*")
            return true;

        var fullName = $"{module}.{actionName}";

        if (IsRegex)
            return Regex.IsMatch(fullName, ActionPattern, RegexOptions.IgnoreCase);

        if (ActionPattern.EndsWith(".*"))
        {
            var prefix = ActionPattern[..^2];
            return module.Equals(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(fullName, ActionPattern, StringComparison.OrdinalIgnoreCase);
    }
}
