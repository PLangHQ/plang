namespace app.Attributes;

/// <summary>
/// Marks a public static method as the vocabulary provider for its declaring type —
/// the closed list of strings the LLM may emit for a slot whose type is this.
///
/// Method shape: <c>public static string[] X(Actor.Context.@this context)</c>. The method
/// name is conventionally <c>Choices</c> but the attribute is the contract. The Context
/// parameter is mandatory even for static vocabularies (signature symmetry — dynamic
/// vocabularies that depend on app/actor state share one shape with static ones).
///
/// Two separate concerns:
///   - Vocabulary (this attribute) — what the LLM may emit. Build-time concern.
///   - Resolution — how the runtime turns the chosen string into a usable object.
///     Lives in the type itself (ctor, lookup, registry — type's choice). Not the
///     language layer's concern.
///
/// Replaces the old <c>static ValidValues</c> + <c>IObject</c> pattern that conflated
/// vocabulary with type construction and broke for stateful runtime types like Actor.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false)]
public sealed class ChoicesAttribute : System.Attribute
{
}
