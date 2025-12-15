namespace PLang.Variables.Errors;

public class InvalidReturnTypeError : VariableMappingErrorBase
{
	public InvalidReturnTypeError(string message)
		: base(message, "InvalidReturnType", "Check that the return type is a valid type name")
	{
	}
}