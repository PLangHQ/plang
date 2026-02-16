using PLang.Building.Model;
using PLang.Runtime2;

namespace PLang.Runtime2.Mapping;

/// <summary>
/// Maps from old Building.Model types to Runtime2 types.
/// </summary>
public static class GoalMapper
{
    /// <summary>
    /// Map PLang.Building.Model.Goal to PLang.Runtime2.Goal
    /// </summary>
    public static Goal ToRuntime2Goal(Building.Model.Goal oldGoal)
    {
        var goal = new Goal
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
            Steps = new Steps(oldGoal.GoalSteps.Select(ToRuntime2Step)),
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
    /// Map PLang.Building.Model.GoalStep to PLang.Runtime2.Step
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
            Actions = new Actions
            {
                new Action
                {
                    Module = ExtractActionName(oldStep.ModuleType),
                    ActionName = oldStep.Name ?? "",
                    Parameters = new(),
                    Return = null
                }
            },
            OnErrorGoal = oldStep.ErrorHandlers?.FirstOrDefault()?.GoalToCall?.Name,
            WaitForExecution = oldStep.WaitForExecution,
            Hash = oldStep.Hash,
            PreviousHash = null, // Computed during serialization
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

    private static Visibility MapVisibility(Building.Model.Visibility visibility)
    {
        return visibility == Building.Model.Visibility.Public
            ? Visibility.Public
            : Visibility.Private;
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

    private static ErrorHandler? MapErrorHandler(Building.Model.ErrorHandler? oldHandler)
    {
        if (oldHandler == null) return null;

        return new ErrorHandler
        {
            Goal = MapGoalToCallInfo(oldHandler.GoalToCall),
            RetryCount = oldHandler.RetryHandler?.RetryCount,
            RetryOverSeconds = oldHandler.RetryHandler?.RetryDelayInMilliseconds != null
                ? (int)(oldHandler.RetryHandler.RetryDelayInMilliseconds.Value / 1000)
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
            Parameters = oldInfo.Parameters,
            PrPath = oldInfo.Path
        };
    }

    private static CacheSettings? MapCacheSettings(Building.Model.CachingHandler? oldCache)
    {
        if (oldCache == null) return null;

        return new CacheSettings
        {
            DurationSeconds = oldCache.TimeInMilliseconds / 1000,
            Sliding = oldCache.CachingType == 0, // 0 = Sliding, 1 = Absolute
            Key = oldCache.CacheKey,
            Location = oldCache.Location
        };
    }
}
