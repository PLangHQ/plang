using PLang.Building.Model;

namespace PLang.Exceptions;

public class RuntimeUserStepException : Exception
{
    public RuntimeUserStepException(string message, string type, int statusCode, GoalStep? step) : base(message)
    {
        Step = step;
        Type = type;
        StatusCode = statusCode;
    }

    public RuntimeUserStepException(GoalStep step, Exception ex, string type = "error", int statusCode = 500) : base(
        $"Step '{step.Text}' had exception", ex)
    {
        Step = step;
        Type = type;
        StatusCode = statusCode;
    }

    public GoalStep? Step { get; set; }
    public string Type { get; set; }
    public int StatusCode { get; set; }
}