namespace PLang.Variables.Errors;

public class MethodNotFoundError : VariableMappingErrorBase
{
	public MethodNotFoundError(string message)
		: base(message, "MethodNotFound", "Check the method name and ensure it exists on the specified class")
	{
	}
}