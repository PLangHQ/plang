using PLang.Building.Model;
using PLang.Runtime2.Core;

namespace PLang.Runtime2.Mapping;

/// <summary>
/// Maps from old Building.Model types to Runtime2.Core types.
/// </summary>
public static class GoalMapper
{
    /// <summary>
    /// Map PLang.Building.Model.Goal to PLang.Runtime2.Core.Goal
    /// </summary>
    public static Core.Goal ToRuntime2Goal(Building.Model.Goal oldGoal)
    {
        var goal = new Core.Goal
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
            Steps = oldGoal.GoalSteps.Select(ToRuntime2Step).ToList(),
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
    /// Map PLang.Building.Model.GoalStep to PLang.Runtime2.Core.Step
    /// </summary>
    public static Core.Step ToRuntime2Step(Building.Model.GoalStep oldStep)
    {
        return new Core.Step
        {
            Index = oldStep.Index,
            Text = oldStep.Text,
            LineNumber = oldStep.LineNumber,
            Indent = oldStep.Indent,
            Comment = oldStep.Comment,
            Actions = new List<Core.IAction>
            {
                new Core.Action
                {
                    Class = ExtractActionName(oldStep.ModuleType),
                    Method = oldStep.Name ?? "",
                    Parameters = new(),
                    Return = new Core.Return()
                }
            },
            OnErrorGoal = oldStep.ErrorHandlers?.FirstOrDefault()?.GoalToCall?.Name,
            WaitForExecution = oldStep.WaitForExecution,
            Hash = oldStep.Hash,
            PreviousHash = null, // Computed during serialization
            Intent = oldStep.UserIntent,
            Data = null, // Set during step building
            OnError = MapErrorHandler(oldStep.ErrorHandlers?.FirstOrDefault()),
            Cache = MapCacheSettings(oldStep.CacheHandler),
            Timeout = oldStep.CancellationHandler?.CancelExecutionAfterXMilliseconds != null
                ? (int)(oldStep.CancellationHandler.CancelExecutionAfterXMilliseconds / 1000)
                : null,
            Errors = oldStep.ValidationErrors?.Select(e => new Core.Info { Text = e.Message }).ToList() ?? new(),
            Warnings = new()
        };
    }

    private static Core.Visibility MapVisibility(Building.Model.Visibility visibility)
    {
        return visibility == Building.Model.Visibility.Public
            ? Core.Visibility.Public
            : Core.Visibility.Private;
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

    private static Core.ErrorHandler? MapErrorHandler(Building.Model.ErrorHandler? oldHandler)
    {
        if (oldHandler == null) return null;

        return new Core.ErrorHandler
        {
            Goal = MapGoalToCallInfo(oldHandler.GoalToCall),
            RetryCount = oldHandler.RetryHandler?.RetryCount,
            RetryOverSeconds = oldHandler.RetryHandler?.RetryDelayInMilliseconds != null
                ? (int)(oldHandler.RetryHandler.RetryDelayInMilliseconds.Value / 1000)
                : null,
            Order = oldHandler.RunRetryBeforeCallingGoalToCall
                ? Core.ErrorOrder.RetryFirst
                : Core.ErrorOrder.GoalFirst,
            IgnoreError = oldHandler.IgnoreError,
            Message = oldHandler.Message,
            StatusCode = oldHandler.StatusCode,
            Key = oldHandler.Key
        };
    }

    private static Core.GoalToCallInfo? MapGoalToCallInfo(PLang.Models.GoalToCallInfo? oldInfo)
    {
        if (oldInfo == null) return null;

        return new Core.GoalToCallInfo
        {
            Name = oldInfo.Name,
            Parameters = oldInfo.Parameters
        };
    }

    private static Core.CacheSettings? MapCacheSettings(Building.Model.CachingHandler? oldCache)
    {
        if (oldCache == null) return null;

        return new Core.CacheSettings
        {
            DurationMinutes = (int)(oldCache.TimeInMilliseconds / 60000),
            Sliding = oldCache.CachingType == 0, // 0 = Sliding, 1 = Absolute
            Key = oldCache.CacheKey,
            Location = oldCache.Location
        };
    }
}
