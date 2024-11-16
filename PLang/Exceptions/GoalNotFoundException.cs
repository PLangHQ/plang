namespace PLang.Exceptions;

public class GoalNotFoundException : Exception
{
    public GoalNotFoundException(string message, string appPath, string goalName) : base(message)
    {
        AppPath = appPath;
        GoalName = goalName;
    }

    public string AppPath { get; private set; }
    public string GoalName { get; private set; }
}