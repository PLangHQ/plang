using System.ComponentModel;
using PLang.Models;

namespace PLang.Building.Model;

public class ErrorHandler
{
    [Attributes.DefaultValue(false)]
    [Description("This will cause the code execution to continue to the next step")]
    public bool IgnoreError { get; set; } = false;

    [Attributes.DefaultValue(null)] public string? Message { get; set; }

    [Attributes.DefaultValue(null)] public int? StatusCode { get; set; }

    [Attributes.DefaultValue(null)]
    [Description("Key can be defined by user")]
    public string? Key { get; set; }

    [Attributes.DefaultValue(null)] public GoalToCall? GoalToCall { get; set; }

    [Attributes.DefaultValue(null)] public Dictionary<string, object?>? GoalToCallParameters { get; set; }

    [Attributes.DefaultValue(null)] public RetryHandler? RetryHandler { get; set; }

    [Attributes.DefaultValue(false)]
    [Description("When user wants to run retry on the step before executing GoalToCall")]
    public bool RunRetryBeforeCallingGoalToCall { get; set; }
}