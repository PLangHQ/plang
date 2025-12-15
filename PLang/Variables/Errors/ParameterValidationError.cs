namespace PLang.Variables.Errors;

public class ParameterValidationError : VariableMappingErrorBase
{
	public ParameterValidationError(string message)
		: base(message, "ParameterValidation", "Review the parameter requirements for this operation")
	{
	}
}