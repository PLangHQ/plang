namespace PLang.Building.Model
{
	public record StepInformation(string StepName, string StepDescription, List<string> Modules);

	public record StepProperties(bool WaitForExecution = true, string? LoggerLevel = null, List<ErrorHandler>? ErrorHandlers = null, CachingHandler? CachingHandler = null);
}
