namespace app.Attributes;

/// <summary>
/// The list property whose elements are records — each a name, a type and a value (<c>goal.call</c>'s
/// <c>Parameter</c>: <c>[{to: item = %user.email%}, {subject: text = "Hi"}]</c>). A step's list in any other slot is
/// what it writes: a list of objects stays a list of objects, whatever its objects look like.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Property)]
public sealed class RecordsAttribute : System.Attribute
{
}
