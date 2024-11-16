namespace PLang.Exceptions;

public class MethodNotFoundException : Exception
{
    public MethodNotFoundException(string message) : base(message)
    {
    }
}