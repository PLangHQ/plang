namespace PLang.Exceptions;

internal class ModuleNotFoundException : Exception
{
    public ModuleNotFoundException(string message) : base(message)
    {
    }
}