namespace app.type;

/// <summary>
/// A named field on a record-kind <see cref="@this"/>.  Lifted from
/// <c>app.builder.type.Field</c> in the Stage 4 Entry-dissolve so the type
/// entity owns its own field-shape vocabulary instead of borrowing from
/// builder.
/// </summary>
public sealed class Field
{
    /// <summary>Field name, lower-camelCase in the catalog (e.g. "actionName").</summary>
    public string Name { get; init; } = "";

    /// <summary>Field type as a catalog name (e.g. "string", "list&lt;action&gt;", "operator").</summary>
    public string TypeName { get; init; } = "";
}
