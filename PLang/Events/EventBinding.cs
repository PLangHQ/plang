using Org.BouncyCastle.Bcpg;
using PLang.Attributes;
using PLang.Building.Model;
using PLang.Models;
using System.Diagnostics;
using System.Text.Json.Serialization;

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
		public const string Module = "Module";

		public const string AppError = "AppError";
        public const string GoalError = "GoalError";
		public const string StepError = "StepError";
		public const string ModuleError = "ModuleError";

	}

	// before each goal in api/* call !DoStuff
	// before each step call !Debugger.SendInfo
	// after Run.goal, call !AfterRun
	public record EventBinding(string EventType, string EventScope, GoalToBindTo GoalToBindTo, GoalToCallInfo GoalToCall,
        [property: DefaultValue("false")] bool IncludePrivate = false,
        int? StepNumber = null, string? StepText = null,
		[property: DefaultValue("true")] bool WaitForExecution = true,
        [property: DefaultValue(null)] string[]? RunOnlyOnStartParameter = null,
        bool OnErrorContinueNextStep = false,
		string? ErrorKey = null, string? ErrorMessage = null, int? StatusCode = null, string? ExceptionType = null, bool IsLocal = false, bool IncludeOsGoals = false, bool IsOnStep = false)
	{
		[LlmIgnore]
		public string Id { get { return $"{EventType}_{EventScope}_{GoalToBindTo?.Name}_{GoalToCall?.Name}_{IncludePrivate}_{StepNumber}_{StepText}_{WaitForExecution}_{string.Join(',', RunOnlyOnStartParameter ?? [""])}_{OnErrorContinueNextStep}_{ErrorKey}_{ErrorMessage}_{StatusCode}_{ExceptionType}_{IsLocal}".ToString(); } }
		[IgnoreWhenInstructed]
		public string UniqueId { get { return Guid.NewGuid().ToString(); } }
		[JsonIgnore]
		public Goal? Goal { get; set; }
		[JsonIgnore]
		public GoalStep? GoalStep { get; set; }
		[JsonIgnore]
		public Instruction? Instruction { get; set; }

		[IgnoreWhenInstructed]
		public Stopwatch Stopwatch { get; set; }
		[JsonIgnore]
		public Goal SourceGoal { get; internal set; }
		[JsonIgnore]
		public GoalStep SourceStep { get; internal set; }
	}

	public record EventModuleBinding(string[] Methods, EventBinding EventBinding) : EventBinding(EventBinding);

    //TODO: Need to create EventBuildBinding, the reason is that it should declare if step should continue to build code or not
    // if step or build event fails, the default behaviour is to build the next step. 
    // this cannot be controlled without EventBuildBinding, which would add ContinueBuild property
    // allow user to define it like this.
    // - before step is build, call AnalyzeStep, do not continue if it fails
}
