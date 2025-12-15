namespace PLang.Variables.Errors;

public class InvalidVariableError : VariableMappingErrorBase
{
	public InvalidVariableError(string message)
		: base(message, "InvalidVariable", "Check variable syntax and ensure it's properly formatted")
	{
	}
}