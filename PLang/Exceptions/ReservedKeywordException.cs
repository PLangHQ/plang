namespace PLang.Exceptions;

public class ReservedKeywordException : Exception
{
    public ReservedKeywordException(string message) : base(message)
    {
    }
}