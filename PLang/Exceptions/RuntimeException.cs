using PLang.Building.Model;

namespace PLang.Exceptions;

public class RuntimeException : Exception
{
    private Goal? goal;

    public RuntimeException(string message, Goal? goal = null, Exception? ex = null) : base(message, ex)
    {
        this.goal = goal;
    }
}