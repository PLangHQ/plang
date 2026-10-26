namespace app.goal.step.action.composed;

/// <summary>
/// An action composed in C# from a seed (<c>new composed(new sign(context) { … }, context)</c>): an operation one owner
/// composes from another's, run as an action so it stays observable. No step holds it in its code, so it runs in a
/// frame of its own kind, of which nothing is left once it has run.
/// </summary>
public sealed class @this : action.@this
{
    /// <summary>plang's own: no step is one.</summary>
    public static new bool Internal => true;

    /// <summary>The action <paramref name="seed"/> is, composed by <paramref name="context"/>'s run.</summary>
    public @this(module.ICodeGenerated seed, actor.context.@this context) : base(seed, context) { }

    internal override global::app.call.@this Frame(global::app.call.list.@this stack, global::app.call.setting.@this setting, Variables? variables)
        => stack.Compose(this, setting, variables);
}
