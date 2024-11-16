namespace PLang.Exceptions;

public class TimeoutException : Exception
{
    public TimeoutException(string message) : base(message)
    {
    }
}