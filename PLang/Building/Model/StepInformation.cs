namespace PLang.Building.Model
{
	public record StepInformation(string StepName, string StepDescription, List<string> Modules)
	{
		public string StepName { get; init; } = StepName.ToLower();
	};

	public record StepProperties(bool WaitForExecution = true, string? LoggerLevel = null, List<ErrorHandler>? ErrorHandlers = null, CachingHandler? CachingHandler = null);
}
