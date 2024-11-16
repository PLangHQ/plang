using PLang.Building.Model;

namespace PLang.Exceptions;

public class RuntimeStepException : BaseStepException
{
    public RuntimeStepException(string message, GoalStep step, Exception? innerException = null) : base(step, message,
        innerException)
    {
    }
}