namespace app.Attributes;

/// <summary>
/// Marks the property of an action that holds a goal to call: <c>goal.call</c>'s Name (a goal of this
/// app's), <c>window.call</c>'s Name (a goal of a page's). The builder checks that every goal a
/// step's words call (<c>call X</c>) is held by one of its actions this way — not only by goal.call.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class GoalAttribute : Attribute;
