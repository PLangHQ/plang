using PLang.Building.Model;
using PLang.Runtime2.Engine;
using Action = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this;
using R2Goal = PLang.Runtime2.Engine.Goals.Goal.@this;
using R2Visibility = PLang.Runtime2.Engine.Goals.Goal.Visibility;
using R2ErrorHandler = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.ErrorHandler;

namespace PLang.Runtime2.Engine.Utility;

/// <summary>
/// Maps from old Building.Model types to Runtime2.Engine types.
/// </summary>
public static class GoalMapper
{
    /// <summary>
    /// Map PLang.Building.Model.Goal to PLang.Runtime2.Engine.Goal
    /// </summary>
    public static R2Goal ToRuntime2Goal(Building.Model.Goal oldGoal)
    {
        var goal = new R2Goal
        {
            Name = oldGoal.GoalName,
            Description = oldGoal.Description,
            Comment = oldGoal.Comment,
            Visibility = MapVisibility(oldGoal.Visibility),
            IsSetup = oldGoal.IsSetup,
            IsEvent = oldGoal.IsEvent,
            Hash = oldGoal.Hash,
            InputParameters = oldGoal.IncomingVariablesRequired,
            Path = oldGoal.RelativeGoalPath,
            PrPath = oldGoal.RelativePrPath,
            Steps = new GoalSteps(oldGoal.GoalSteps.Select(ToRuntime2Step)),
            SubGoals = oldGoal.SubGoals ?? new(),
            Errors = new(),
            Warnings = new()
        };

        // Set goal reference on steps
        foreach (var step in goal.Steps)
        {
            step.Goal = goal;
        }

        return goal;
    }

    /// <summary>
    /// Map PLang.Building.Model.GoalStep to PLang.Runtime2.Engine.Step
    /// </summary>
    public static Step ToRuntime2Step(Building.Model.GoalStep oldStep)
    {
        return new Step
        {
            Index = oldStep.Index,
            Text = oldStep.Text,
            LineNumber = oldStep.LineNumber,
            Indent = oldStep.Indent,
            Comment = oldStep.Comment,
            Actions = new StepActions
            {
                new Action
                {
                    Module = ExtractActionName(oldStep.ModuleType),
                    ActionName = oldStep.Name ?? "",
                    Parameters = new(),
                    Return = null
                }
            },
            WaitForExecution = oldStep.WaitForExecution,
            Hash = oldStep.Hash,
            Intent = oldStep.UserIntent,
            OnError = MapErrorHandler(oldStep.ErrorHandlers?.FirstOrDefault()),
            Cache = MapCacheSettings(oldStep.CacheHandler),
            Timeout = oldStep.CancellationHandler?.CancelExecutionAfterXMilliseconds != null
                ? (int)(oldStep.CancellationHandler.CancelExecutionAfterXMilliseconds / 1000)
                : null,
            Errors = oldStep.ValidationErrors?.Select(e => new Info { Message = e.Message }).ToList() ?? new(),
            Warnings = new()
        };
    }

    private static R2Visibility MapVisibility(Building.Model.Visibility visibility)
    {
        return visibility == Building.Model.Visibility.Public
            ? R2Visibility.Public
            : R2Visibility.Private;
    }

    private static string ExtractActionName(string? moduleType)
    {
        if (string.IsNullOrEmpty(moduleType))
            return "";

        // Convert "PLang.Modules.HttpModule" to "http"
        var name = moduleType
            .Replace("PLang.Modules.", "")
            .Replace("Module", "")
            .Replace(".Program", "");

        return name.ToLowerInvariant();
    }

    private static R2ErrorHandler? MapErrorHandler(Building.Model.ErrorHandler? oldHandler)
    {
        if (oldHandler == null) return null;

        return new R2ErrorHandler
        {
            Goal = MapGoalToCallInfo(oldHandler.GoalToCall),
            RetryCount = oldHandler.RetryHandler?.RetryCount,
            RetryOverMs = oldHandler.RetryHandler?.RetryDelayInMilliseconds != null
                ? (int)oldHandler.RetryHandler.RetryDelayInMilliseconds.Value
                : null,
            Order = oldHandler.RunRetryBeforeCallingGoalToCall
                ? ErrorOrder.RetryFirst
                : ErrorOrder.GoalFirst,
            IgnoreError = oldHandler.IgnoreError,
            Message = oldHandler.Message,
            StatusCode = oldHandler.StatusCode,
            Key = oldHandler.Key
        };
    }

    private static GoalCall? MapGoalToCallInfo(PLang.Models.GoalToCallInfo? oldInfo)
    {
        if (oldInfo == null) return null;

        return new GoalCall
        {
            Name = oldInfo.Name,
            Parameters = oldInfo.Parameters?
                .Select(p => new Memory.Data(p.Key, p.Value))
                .ToList(),
            PrPath = oldInfo.Path
        };
    }

    private static CacheSettings? MapCacheSettings(Building.Model.CachingHandler? oldCache)
    {
        if (oldCache == null) return null;

        return new CacheSettings
        {
            DurationMs = oldCache.TimeInMilliseconds,
            Sliding = oldCache.CachingType == 0, // 0 = Sliding, 1 = Absolute
            Key = oldCache.CacheKey,
            Location = oldCache.Location
        };
    }
}
