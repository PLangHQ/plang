namespace app.Attributes;

/// <summary>
/// The property an action reads and answers a new value of, changing nothing (<c>list.query</c>'s <c>List</c>):
/// a step that names no destination writes the answer back into it. The builder asks the action for it
/// (<c>action.Input</c>), never the handler.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Property)]
public sealed class InputAttribute : System.Attribute
{
}
