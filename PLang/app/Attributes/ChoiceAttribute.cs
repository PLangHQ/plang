namespace app.Attributes;

/// <summary>
/// This class is a choice — a closed set a <c>choice&lt;T&gt;</c> draws on: an enum's members, a class naming its own
/// (a static <c>Choices(context?)</c>), or a kind family's kinds. The set's name is the class's plang name, as a set
/// declares it. The types read the marked classes when an app is born, as they read <c>[Format]</c>; a
/// <c>choice&lt;T&gt;</c> over a class that isn't marked is a test failure, never a walk at birth.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Enum, Inherited = false)]
public sealed class ChoiceAttribute : System.Attribute
{
}
