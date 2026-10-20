namespace PLang.Tests.Shared;

using Step = global::app.goal.step.@this;
using Action = global::app.goal.step.action.@this;

/// <summary>Test-only collection-initializer sugar for a goal's steps — a plain <see cref="List{Step}"/>
/// that implicitly becomes the immutable <c>step.list</c> node the graph holds. Lets tests keep writing
/// <c>Step = new GoalSteps { new Step { … }, … }</c> after the runtime moved to node storage.</summary>
public sealed class GoalSteps : System.Collections.Generic.List<Step>
{
    // the node's type, asked of the app's type list when the sugar is made
    private readonly global::app.type.@this _type;
    public GoalSteps(global::app.type.list.@this types) => _type = types[typeof(global::app.goal.step.list.@this)];
    public GoalSteps(global::app.type.list.@this types, System.Collections.Generic.IEnumerable<Step> steps) : base(steps)
        => _type = types[typeof(global::app.goal.step.list.@this)];
    public static implicit operator global::app.goal.step.list.@this(GoalSteps s)
    {
        var node = new global::app.goal.step.list.@this(s._type);
        foreach (var step in s) node.Add(step);
        return node;
    }
    public System.Threading.Tasks.ValueTask<global::app.data.@this> Start(global::app.actor.context.@this context)
        => ((global::app.goal.step.list.@this)this).Start(context);
}

/// <summary>Test-only collection-initializer sugar for a step's actions — implicitly becomes the
/// immutable <c>action.list</c> node.</summary>
public sealed class StepActions : System.Collections.Generic.List<Action>
{
    // the node's type, asked of the app's type list when the sugar is made
    private readonly global::app.type.@this _type;
    public StepActions(global::app.type.list.@this types) => _type = types[typeof(global::app.goal.step.action.list.@this)];
    public StepActions(global::app.type.list.@this types, System.Collections.Generic.IEnumerable<Action> actions) : base(actions)
        => _type = types[typeof(global::app.goal.step.action.list.@this)];
    public static implicit operator global::app.goal.step.action.list.@this(StepActions a)
    {
        var node = new global::app.goal.step.action.list.@this(a._type);
        foreach (var action in a) node.Add(action);
        return node;
    }
}
