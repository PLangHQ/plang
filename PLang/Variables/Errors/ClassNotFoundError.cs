namespace PLang.Variables.Errors;

public class ClassNotFoundError : VariableMappingErrorBase
{
	public ClassNotFoundError(string message)
		: base(message, "ClassNotFound", "Verify the class name and ensure it has the [Piped] attribute")
	{
	}
}