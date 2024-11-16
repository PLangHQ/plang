using PLang.Building.Model;

namespace PLang.Exceptions;

public class RuntimeGoalEndException : Exception
{
    public RuntimeGoalEndException(string? message, GoalStep? step) : base(message)
    {
        Step = step;
    }

    public RuntimeGoalEndException(GoalStep? step, Exception? ex) : base($"Step '{step?.Text}' ended goal", ex)
    {
        Step = step;
    }

    public GoalStep? Step { get; set; }
}