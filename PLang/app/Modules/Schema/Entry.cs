namespace app.Modules.Schema;

/// <summary>
/// A type in the PLang catalog. Discriminated by <see cref="Kind"/>:
///   Record — has <see cref="Fields"/>, no Values.
///   Enum   — has <see cref="Values"/>, no Fields.
///   Scalar — wraps a primitive shape (e.g. <c>path</c> = a string with semantics);
///            uses <see cref="Shape"/> instead of Fields/Values.
/// All kinds may carry a <see cref="Description"/> and <see cref="Example"/> declared
/// on the type's <c>[PlangType]</c> attribute — the type teaches the LLM about itself.
/// </summary>
public sealed class Entry
{
    /// <summary>The type's PLang name (goal, step, operator, ...).</summary>
    public string Name { get; init; } = "";

    /// <summary>Record / Enum / Scalar — which side of the union is populated.</summary>
    public EntryKind Kind { get; init; }

    /// <summary>Record fields. Null when Kind is not Record.</summary>
    public IReadOnlyList<Field>? Fields { get; init; }

    /// <summary>Enum values. Null when Kind is not Enum.</summary>
    public IReadOnlyList<string>? Values { get; init; }

    /// <summary>
    /// Read-only navigation properties for Scalar types — what's accessible via
    /// <c>%var.Property%</c> at runtime. Different from <see cref="Fields"/>
    /// (which describes Record construction): these are computed views on a
    /// constructed value, not constructor inputs.
    /// Null for Record / Enum.
    /// </summary>
    public IReadOnlyList<Field>? Properties { get; init; }

    /// <summary>
    /// Wire shape for Scalar types — the underlying primitive form (e.g. "string"
    /// for <c>path</c>). Sourced from a static <c>Resolve(input, context)</c>
    /// method on the type when present (the source-generator convention) or from
    /// <c>[PlangType(Shape = ...)]</c>. Null for Record / Enum.
    /// </summary>
    public string? Shape { get; init; }

    /// <summary>
    /// Constructor signature for Scalar types — <c>"name: shape"</c> when sourced
    /// from a static <c>Resolve(input, context)</c> method. Lets the catalog teach
    /// the LLM exactly what a parameter value of this type carries (e.g.
    /// <c>"rawPath: string"</c>) without exposing internal C# constructor noise.
    /// Null when the type only declares <c>Shape</c>.
    /// </summary>
    public string? ConstructorSignature { get; init; }

    /// <summary>Canonical example value, from <c>[PlangType(Example = ...)]</c>. Optional.</summary>
    public string? Example { get; init; }

    /// <summary>Semantic description, from <c>[PlangType(Description = ...)]</c>. Optional.</summary>
    public string? Description { get; init; }

    /// <summary>The underlying CLR type, preserved for consumers that need it (e.g. deserialization).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public System.Type? ClrType { get; init; }
}

/// <summary>A field on a Record-kind <see cref="Entry"/>.</summary>
public sealed class Field
{
    /// <summary>Field name, lower-camelCase in the catalog (e.g. "actionName").</summary>
    public string Name { get; init; } = "";

    /// <summary>Field type as a catalog name (e.g. "string", "list<action>", "operator").</summary>
    public string TypeName { get; init; } = "";
}

public enum EntryKind
{
    Record,
    Enum,
    Scalar,
}
