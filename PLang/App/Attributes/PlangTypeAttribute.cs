namespace App.Attributes;

/// <summary>
/// Declares a type's public identity in the PLang catalog — the name the builder/LLM
/// and deserialization use to refer to it. A type owns its own name; TypeMapping only
/// looks it up.
///
/// Usage:
///   [PlangType]                      // name inferred — @this classes use the last
///                                    //   namespace segment, others use the class
///                                    //   name lowercased.
///   [PlangType("goal.call")]         // explicit name (required when the inferred
///                                    //   name can't be spelled — e.g. contains a
///                                    //   dot, or the class name doesn't match the
///                                    //   PLang convention).
///   [PlangType("tstring")]           // multiple attributes allowed for aliases.
///   [PlangType("translatable")]      //   First attribute wins as the canonical
///                                    //   display name; others resolve on lookup.
///
/// Rules:
///   - A type without [PlangType] is opaque to the catalog unless it is an @this
///     class (the OBP convention makes those catalog-visible by default) or an enum
///     that appears as an action parameter type.
///   - Catalog shape comes from [LlmBuilder] on properties when present. Types that
///     are scalars (e.g. <c>path</c> = a string) declare their shape directly here
///     via <see cref="Shape"/> + <see cref="Example"/> + <see cref="Description"/>;
///     the type catalog renders them so the LLM never has to infer the value form.
/// </summary>
[System.AttributeUsage(
    System.AttributeTargets.Class | System.AttributeTargets.Struct | System.AttributeTargets.Enum,
    AllowMultiple = true)]
public sealed class PlangTypeAttribute : System.Attribute
{
    /// <summary>
    /// Explicit name override. Null means "infer from conventions".
    /// When multiple [PlangType] attributes are present, the first one with a Name
    /// is the canonical display name; the rest act as lookup aliases.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// The wire shape the LLM emits for values of this type — e.g. "string" for
    /// <c>path</c>. When set, the type appears in the catalog's "Types referenced"
    /// block as <c>name: Shape — Description (e.g. Example)</c>. Use this for
    /// scalar domain types (Path, GoalCall name) that wrap a primitive value.
    /// </summary>
    public string? Shape { get; init; }

    /// <summary>
    /// Canonical example value the LLM should mimic — e.g. "/some/file.json" for
    /// <c>path</c>. Rendered in the catalog Types block to anchor the LLM on a
    /// concrete shape and prevent reflection-based hallucination.
    /// </summary>
    public string? Example { get; init; }

    /// <summary>
    /// One-sentence semantic teaching the LLM needs that isn't visible from the
    /// shape alone — e.g. "relative paths resolve against the calling goal's
    /// folder". Rendered next to the type in the catalog.
    /// </summary>
    public string? Description { get; init; }

    public PlangTypeAttribute() { }
    public PlangTypeAttribute(string name) { Name = name; }
}
