namespace PLang.Exceptions;

public class RunGoalException : Exception
{
    public RunGoalException(string goalName, Exception ex) : base(goalName, ex)
    {
    }
}