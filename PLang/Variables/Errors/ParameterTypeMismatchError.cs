namespace PLang.Variables.Errors;

public class ParameterTypeMismatchError : VariableMappingErrorBase
{
	public ParameterTypeMismatchError(string message)
		: base(message, "ParameterTypeMismatch", "Ensure parameter types match the expected types")
	{
	}
}