namespace PLang.Exceptions;

public class FunctionNotFoundException : Exception
{
    public FunctionNotFoundException(string moduleName) : base(moduleName)
    {
    }
}