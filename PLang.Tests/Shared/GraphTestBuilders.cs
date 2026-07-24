namespace PLang.Tests.Shared;

using Step = global::app.goal.step.@this;
using Action = global::app.goal.step.action.@this;

/// <summary>Test-only collection-initializer sugar for a goal's steps — a plain <see cref="List{Step}"/>
/// that implicitly becomes the immutable <c>step.list</c> node the graph holds. Lets tests keep writing
/// <c>Step = new GoalSteps { new Step { … }, … }</c> after the runtime moved to node storage.</summary>
public sealed class GoalSteps : System.Collections.Generic.List<Step>
{
    public GoalSteps() { }
    public GoalSteps(System.Collections.Generic.IEnumerable<Step> steps) : base(steps) { }
    public static implicit operator global::app.goal.step.list.@this(GoalSteps s)
    {
        var node = new global::app.goal.step.list.@this();
        foreach (var step in s) node.Add(step);
        return node;
    }
    public System.Threading.Tasks.Task<global::app.data.@this> Run(global::app.actor.context.@this context)
        => ((global::app.goal.step.list.@this)this).Run(context);
}

/// <summary>Test-only collection-initializer sugar for a step's actions — implicitly becomes the
/// immutable <c>action.list</c> node.</summary>
public sealed class StepActions : System.Collections.Generic.List<Action>
{
    public StepActions() { }
    public StepActions(System.Collections.Generic.IEnumerable<Action> actions) : base(actions) { }
    public static implicit operator global::app.goal.step.action.list.@this(StepActions a)
    {
        var node = new global::app.goal.step.action.list.@this();
        foreach (var action in a) node.Add(action);
        return node;
    }
}
