namespace PLang.Variables.Errors;

public class VariableNotFoundError : VariableMappingErrorBase
{
	public VariableNotFoundError(string message)
		: base(message, "VariableNotFound", "Ensure the variable expression matches exactly what appears in the original text")
	{
	}
}