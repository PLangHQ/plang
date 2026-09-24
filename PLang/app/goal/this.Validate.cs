namespace app.goal;

// The goal judges itself: each step and each sub-goal gives its own verdict, and the goal gathers
// them into one error. Pure judgement — it runs after construction and changes nothing.
public sealed partial class @this
{
    /// <summary>What is wrong with this goal, or null when nothing is.</summary>
    public async System.Threading.Tasks.Task<global::app.error.Error?> Validate(
        global::app.actor.context.@this context)
    {
        var causes = new System.Collections.Generic.List<global::app.error.Error>();
        for (int i = 0; i < Step.Count; i++)
            if (await Step[i].Validate(context) is { } invalid) causes.Add(invalid);
        foreach (var child in Child)
            if (await child.Validate(context) is { } invalid) causes.Add(invalid);

        if (causes.Count == 0) return null;
        return new global::app.error.Error(
            $"goal '{Name}': {string.Join("; ", causes.Select(c => c.Message))}", "GoalInvalid", 400) { list = causes };
    }
}
