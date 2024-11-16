namespace PLang.Exceptions;

public class VariableDoesNotExistsException : Exception
{
    public VariableDoesNotExistsException(string message) : base(message)
    {
    }
}