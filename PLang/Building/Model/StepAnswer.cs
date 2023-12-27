using Microsoft.CodeAnalysis;

namespace PLang.Building.Model
{
	public record StepAnswer(string StepName, string StepDescription, List<string> Modules,
		bool WaitForExecution = true, CachingHandler? CachingHandler = null, ErrorHandler? ErrorHandler = null, RetryHandler? RetryHandler = null);

}
