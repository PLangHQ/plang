using static PLang.Modules.BaseBuilder;

namespace PLang.Errors
{
	public record Error(string key, string message, Error? innerError = null, Exception? exception = null)
	{
		public string Key { get; } = key;
		public string Message { get; } = message;
		public Error? InnerError { get; } = innerError;
		public Exception? Exception { get; } = exception;
	}

	public record StepError(string key, string message, string functionName, List<Parameter> parameters, List<ReturnValue>? returnValue = null) : Error(key, message)
	{
		public string FunctionName { get; } = functionName;
		public List<Parameter> Parameters { get; } = parameters;
		public List<ReturnValue>? ReturnValues { get; } = returnValue;

	}
}
