namespace PLang.Building.Model
{
	public record StepInformation(string ExplainUserIntent, string Reason, string StepName, string StepDescription, List<string> Modules, string Confidence, string Inconsistency)
	{
		public string StepName { get; init; } = StepName.ToLower();
	};

	public record StepProperties(string Reasoning, bool WaitForExecution = true, string? LoggerLevel = null, List<ErrorHandler>? ErrorHandlers = null, CachingHandler? CachingHandler = null);
}
