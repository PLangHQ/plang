using PLang.Attributes;
using PLang.Models;

namespace PLang.Events
{
    public static class EventType
    {
        public const string Before = "Before";
        public const string After = "After";
    }

    public static class VariableEventType
    {
        public const string OnCreate = "OnCreate";
        public const string OnChange = "OnChange";
        public const string OnRemove = "OnRemove";
    }

    public static class EventScope
    {
        public const string StartOfApp = "StartOfApp";
        public const string EndOfApp = "EndOfApp";
        public const string RunningApp = "RunningApp";

        public const string Goal = "Goal";
        public const string Step = "Step";

        public const string AppError = "AppError";
        public const string GoalError = "GoalError";
        public const string StepError = "StepError";

    }

    // before each goal in api/* call !DoStuff
    // before each step call !Debugger.SendInfo
    // after Run.goal, call !AfterRun
    public record EventBinding(string EventType, string EventScope, GoalToCall GoalToBindTo, GoalToCall GoalToCall,
        [property: DefaultValue("false")] bool IncludePrivate = false,
        int? StepNumber = null, string? StepText = null,
        [property: DefaultValue("true")] bool WaitForExecution = true,
        [property: DefaultValue(null)] string[]? RunOnlyOnStartParameter = null,
        bool OnErrorContinueNextStep = false);

    //TODO: Need to create EventBuildBinding, the reason is that it should declare if step should continue to build code or not
    // if step or build event fails, the default behaviour is to build the next step. 
    // this cannot be controlled without EventBuildBinding, which would add ContinueBuild property
    // allow user to define it like this.
    // - before step is build, call AnalyzeStep, do not continue if it fails
}
