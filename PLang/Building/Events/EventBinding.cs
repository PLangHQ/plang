namespace PLang.Building.Events
{
	public enum EventType
    {
        Before = 0, After = 1, OnError = 2
    }

    public enum VariableEventType
    {
        OnCreate = 0,
        OnChange = 1,
        OnRemove = 2
    }

    public enum EventScope
    {
        StartOfApp = 0,
        EndOfApp = 1,
        RunningApp = 2,

        Goal = 20,
        Step = 30
    }

	// before each goal in api/* call !DoStuff
	// before each step call !Debugger.SendInfo
    // after Run.goal, call !AfterRun
	public record EventBinding(EventType EventType, EventScope EventScope, string GoalToBindTo, string GoalToCall,
	    [property: DefaultValue("false")] bool IncludePrivate = false, 
        int? StepNumber = null, string? StepText = null,
		[property: DefaultValue("true")] bool WaitForExecution = true,
		[property: DefaultValue("false")] bool RunOnlyInDebugMode = false);
}
