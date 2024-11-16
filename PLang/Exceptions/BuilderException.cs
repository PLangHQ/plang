using PLang.Building.Model;

namespace PLang.Exceptions;

public class BuilderException : Exception
{
    public BuilderException(string message, Goal? goal = null) : base(message)
    {
        Goal = goal;
    }

    public Goal? Goal { get; set; }
}

public class BuilderStepException : BaseStepException
{
    public BuilderStepException(string message, GoalStep? step = null, Exception? innerException = null) : base(step,
        message, innerException)
    {
    }
}