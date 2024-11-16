namespace PLang.Exceptions;

public class ParsingException : Exception
{
    public ParsingException(string message, Exception ex) : base(message, ex)
    {
    }
}