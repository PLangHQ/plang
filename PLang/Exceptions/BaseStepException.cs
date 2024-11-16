using PLang.Building.Model;
using PLang.Utils;

namespace PLang.Exceptions;

public abstract class BaseStepException : Exception
{
    protected BaseStepException(GoalStep step, string message, Exception? innerException) : base(message,
        innerException)
    {
        Step = step;
    }

    public GoalStep Step { get; }

    public object ToFormat()
    {
        var innerEx = "";
        var ex = InnerException;
        if (ex != null && ex is BaseStepException) innerEx = ex.ToString();

        var error = "";
        AppContext.TryGetSwitch(ReservedKeywords.Debug, out var isDebug);
        if (!isDebug) AppContext.TryGetSwitch(ReservedKeywords.CSharpDebug, out isDebug);

        if (isDebug && Step != null)
        {
            var errorDetail = "";
            var errorInfo = @"
👇
Called from";
            if (string.IsNullOrEmpty(innerEx))
            {
                errorDetail = @$"------
	Error: {Message}
	StackTrace: {StackTrace}";
                errorInfo = "Error happend at";
            }

            error += $@"
{innerEx}

{errorInfo}
	Step: {Step.Text} line {Step.LineNumber + 1}
	Goal: {Step.Goal.GoalName} in {Step.Goal.GoalFileName}
	{errorDetail}";
        }
        else
        {
            if (!string.IsNullOrEmpty(innerEx)) error = innerEx;
            if (Step != null)
                error += $@"
{Message} in line {Step.LineNumber + 1} in {Step.Goal.GoalName} at file {Step.Goal.GoalFileName} 👇

";
            else
                error += Message;
        }

        return error.Trim();
    }
}