namespace app.Attributes;

/// <summary>
/// The type this class is has kinds, and its subclasses are those kinds — never types of their own: <c>path</c>
/// (a file, an http path), <c>setting</c> (each class of settings), <c>list</c> (a list of one element type),
/// <c>query</c> (a where, a group, …). A kind is read as its type with the kind's name (<c>{path, kind: file}</c>).
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Class, Inherited = false)]
public sealed class KindsAttribute : System.Attribute
{
}
