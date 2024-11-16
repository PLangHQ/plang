using PLang.Building.Model;

namespace PLang.Exceptions;

public class ParameterException : Exception
{
    public ParameterException(string message, GoalStep? step, Exception? ex = null) : base(message, ex)
    {
        Step = step;
    }

    public GoalStep? Step { get; set; }
}