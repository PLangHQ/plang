namespace PLang.Models;

public class GoalToCall
{
    public GoalToCall(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) Value = value.Replace("!", "");
    }

    public string? Value { get; }

    public override string? ToString()
    {
        return Value;
    }

    // Implicit conversion from string to GoalToCall
    public static implicit operator GoalToCall(string? value)
    {
        return new GoalToCall(value);
    }

    // Implicit conversion from GoalToCall to string
    public static implicit operator string?(GoalToCall goalToCall)
    {
        return goalToCall.Value;
    }
}