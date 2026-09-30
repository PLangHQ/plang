namespace app.Attributes;

/// <summary>
/// Declares that an action calls a goal, and which of its properties names it: <c>goal.call</c>
/// (a goal of this app's), <c>browser.callGoal</c> (a goal of a page's). The builder checks that every
/// goal a step's words call (<c>call X</c>) is called by one of its actions — any action that declares
/// this, not only goal.call.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class CallsGoalAttribute(string property) : Attribute
{
    /// <summary>The property that holds the called goal's name.</summary>
    public string Property { get; } = property;
}
