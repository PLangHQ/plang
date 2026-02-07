using System.Text.Json.Serialization;
using PLang.Runtime2.Core;
using PLang.Runtime2.Memory;

namespace PLang.Runtime2.Utility;

/// <summary>
/// DTO for deserializing .pr files (compiled goals).
/// </summary>
public sealed class GoalData
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("comment")]
    public string? Comment { get; set; }

    [JsonPropertyName("visibility")]
    public string? Visibility { get; set; }

    [JsonPropertyName("isSetup")]
    public bool IsSetup { get; set; }

    [JsonPropertyName("isEvent")]
    public bool IsEvent { get; set; }

    [JsonPropertyName("hash")]
    public string? Hash { get; set; }

    [JsonPropertyName("inputParameters")]
    public Dictionary<string, string>? InputParameters { get; set; }

    [JsonPropertyName("steps")]
    public List<StepDataDto> Steps { get; set; } = new();

    [JsonPropertyName("subGoals")]
    public List<string>? SubGoals { get; set; }

    [JsonPropertyName("errors")]
    public List<Info>? Errors { get; set; }

    [JsonPropertyName("warnings")]
    public List<Info>? Warnings { get; set; }
}

/// <summary>
/// DTO for error handler in .pr files.
/// </summary>
public sealed class ErrorHandlerData
{
    [JsonPropertyName("goal")]
    public GoalToCallInfoData? Goal { get; set; }

    [JsonPropertyName("retryCount")]
    public int? RetryCount { get; set; }

    [JsonPropertyName("retryOverSeconds")]
    public int? RetryOverSeconds { get; set; }

    [JsonPropertyName("order")]
    public string? Order { get; set; }

    [JsonPropertyName("ignoreError")]
    public bool IgnoreError { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("statusCode")]
    public int? StatusCode { get; set; }

    [JsonPropertyName("key")]
    public string? Key { get; set; }
}

/// <summary>
/// DTO for goal-to-call info in .pr files.
/// </summary>
public sealed class GoalToCallInfoData
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("parameters")]
    public Dictionary<string, object?>? Parameters { get; set; }
}

/// <summary>
/// DTO for cache settings in .pr files.
/// </summary>
public sealed class CacheSettingsData
{
    [JsonPropertyName("durationMinutes")]
    public int DurationMinutes { get; set; }

    [JsonPropertyName("sliding")]
    public bool Sliding { get; set; }

    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }
}

/// <summary>
/// DTO for Data serialization in .pr files.
/// </summary>
public sealed class DataDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("value")]
    public object? Value { get; set; }
}

/// <summary>
/// DTO for step data in .pr files.
/// </summary>
public sealed class StepDataDto
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    [JsonPropertyName("lineNumber")]
    public int LineNumber { get; set; }

    [JsonPropertyName("indent")]
    public int Indent { get; set; }

    [JsonPropertyName("comment")]
    public string? Comment { get; set; }

    [JsonPropertyName("actions")]
    public List<Core.Action> Actions { get; set; } = new();

    [JsonPropertyName("onErrorGoal")]
    public string? OnErrorGoal { get; set; }

    [JsonPropertyName("waitForExecution")]
    public bool WaitForExecution { get; set; } = true;

    [JsonPropertyName("hash")]
    public string? Hash { get; set; }

    [JsonPropertyName("previousHash")]
    public string? PreviousHash { get; set; }

    [JsonPropertyName("intent")]
    public string? Intent { get; set; }

    [JsonPropertyName("onError")]
    public ErrorHandlerData? OnError { get; set; }

    [JsonPropertyName("cache")]
    public CacheSettingsData? Cache { get; set; }

    [JsonPropertyName("timeout")]
    public int? Timeout { get; set; }

    [JsonPropertyName("errors")]
    public List<Info>? Errors { get; set; }

    [JsonPropertyName("warnings")]
    public List<Info>? Warnings { get; set; }
}

/// <summary>
/// Helper for converting between DTOs and runtime objects.
/// </summary>
public static class GoalDataConverter
{
    public static Goal ToGoal(GoalData data, string? path = null, string? prPath = null)
    {
        var goal = new Goal
        {
            Name = data.Name,
            Description = data.Description,
            Comment = data.Comment,
            Visibility = ParseVisibility(data.Visibility),
            IsSetup = data.IsSetup,
            IsEvent = data.IsEvent,
            Hash = data.Hash,
            InputParameters = data.InputParameters,
            Path = path,
            PrPath = prPath,
            Steps = new Steps(data.Steps.Select(ToStep)),
            SubGoals = data.SubGoals ?? new(),
            Errors = data.Errors ?? new(),
            Warnings = data.Warnings ?? new()
        };

        // Set goal reference on steps
        foreach (var step in goal.Steps)
        {
            step.Goal = goal;
        }

        return goal;
    }

    public static Step ToStep(StepDataDto data)
    {
        return new Step
        {
            Index = data.Index,
            Text = data.Text,
            LineNumber = data.LineNumber,
            Indent = data.Indent,
            Comment = data.Comment,
            Actions = new Actions(data.Actions),
            OnErrorGoal = data.OnErrorGoal,
            WaitForExecution = data.WaitForExecution,
            Hash = data.Hash,
            PreviousHash = data.PreviousHash,
            Intent = data.Intent,
            OnError = ToErrorHandler(data.OnError),
            Cache = ToCacheSettings(data.Cache),
            Timeout = data.Timeout,
            Errors = data.Errors ?? new(),
            Warnings = data.Warnings ?? new()
        };
    }

    public static GoalData ToData(Goal goal)
    {
        return new GoalData
        {
            Name = goal.Name,
            Description = goal.Description,
            Comment = goal.Comment,
            Visibility = goal.Visibility.ToString().ToLowerInvariant(),
            IsSetup = goal.IsSetup,
            IsEvent = goal.IsEvent,
            Hash = goal.Hash,
            InputParameters = goal.InputParameters,
            Steps = goal.Steps.Select(ToData).ToList(),
            SubGoals = goal.SubGoals,
            Errors = goal.Errors.Count > 0 ? goal.Errors : null,
            Warnings = goal.Warnings.Count > 0 ? goal.Warnings : null
        };
    }

    public static StepDataDto ToData(Step step)
    {
        return new StepDataDto
        {
            Index = step.Index,
            Text = step.Text,
            LineNumber = step.LineNumber,
            Indent = step.Indent,
            Comment = step.Comment,
            Actions = step.Actions.Select(a => new Core.Action
            {
                Class = a.Class,
                Method = a.Method,
                Parameters = new List<Data>(a.Parameters),
                Return = a.Return != null ? new List<Data>(a.Return) : null
            }).ToList(),
            OnErrorGoal = step.OnErrorGoal,
            WaitForExecution = step.WaitForExecution,
            Hash = step.Hash,
            PreviousHash = step.PreviousHash,
            Intent = step.Intent,
            OnError = ToData(step.OnError),
            Cache = ToData(step.Cache),
            Timeout = step.Timeout,
            Errors = step.Errors.Count > 0 ? step.Errors : null,
            Warnings = step.Warnings.Count > 0 ? step.Warnings : null
        };
    }

    private static Data? ToData(DataDto? data)
    {
        if (data == null) return null;
        var typeInfo = !string.IsNullOrEmpty(data.Type) ? TypeInfo.FromName(data.Type) : null;
        return new Data(data.Name, data.Value, typeInfo);
    }

    private static DataDto? ToDataDto(Data? value)
    {
        if (value == null) return null;
        return new DataDto
        {
            Name = value.Name,
            Type = value.TypeInfo?.Name,
            Value = value.Value
        };
    }

    private static ErrorHandler? ToErrorHandler(ErrorHandlerData? data)
    {
        if (data == null) return null;
        return new ErrorHandler
        {
            Goal = ToGoalToCallInfo(data.Goal),
            RetryCount = data.RetryCount,
            RetryOverSeconds = data.RetryOverSeconds,
            Order = ParseErrorOrder(data.Order),
            IgnoreError = data.IgnoreError,
            Message = data.Message,
            StatusCode = data.StatusCode,
            Key = data.Key
        };
    }

    private static ErrorHandlerData? ToData(ErrorHandler? handler)
    {
        if (handler == null) return null;
        return new ErrorHandlerData
        {
            Goal = ToData(handler.Goal),
            RetryCount = handler.RetryCount,
            RetryOverSeconds = handler.RetryOverSeconds,
            Order = handler.Order?.ToString().ToLowerInvariant(),
            IgnoreError = handler.IgnoreError,
            Message = handler.Message,
            StatusCode = handler.StatusCode,
            Key = handler.Key
        };
    }

    private static Core.GoalToCallInfo? ToGoalToCallInfo(GoalToCallInfoData? data)
    {
        if (data == null) return null;
        return new Core.GoalToCallInfo
        {
            Name = data.Name,
            Parameters = data.Parameters
        };
    }

    private static GoalToCallInfoData? ToData(Core.GoalToCallInfo? info)
    {
        if (info == null) return null;
        return new GoalToCallInfoData
        {
            Name = info.Name,
            Parameters = info.Parameters
        };
    }

    private static CacheSettings? ToCacheSettings(CacheSettingsData? data)
    {
        if (data == null) return null;
        return new CacheSettings
        {
            DurationMinutes = data.DurationMinutes,
            Sliding = data.Sliding,
            Key = data.Key,
            Location = data.Location
        };
    }

    private static CacheSettingsData? ToData(CacheSettings? settings)
    {
        if (settings == null) return null;
        return new CacheSettingsData
        {
            DurationMinutes = settings.DurationMinutes,
            Sliding = settings.Sliding,
            Key = settings.Key,
            Location = settings.Location
        };
    }

    private static Visibility ParseVisibility(string? visibility)
    {
        if (string.IsNullOrEmpty(visibility))
            return Visibility.Private;

        return visibility.Equals("public", StringComparison.OrdinalIgnoreCase)
            ? Visibility.Public
            : Visibility.Private;
    }

    private static ErrorOrder? ParseErrorOrder(string? order)
    {
        if (string.IsNullOrEmpty(order))
            return null;

        return order.Equals("retryfirst", StringComparison.OrdinalIgnoreCase)
            ? ErrorOrder.RetryFirst
            : ErrorOrder.GoalFirst;
    }
}
