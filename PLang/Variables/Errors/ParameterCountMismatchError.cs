namespace PLang.Variables.Errors;

public class ParameterCountMismatchError : VariableMappingErrorBase
{
	public ParameterCountMismatchError(string message)
		: base(message, "ParameterCountMismatch", "Verify the number of parameters matches the method signature")
	{
	}
}