namespace app.type.item.list.kind.element;

/// <summary>
/// A kind of list named by its element's type — <c>list&lt;path&gt;</c> is <c>{list, path}</c>. It carries the
/// element's class, and a list of it is <c>list&lt;T&gt;</c> closed over that class (a choice set closes
/// <c>choice&lt;T&gt;</c> the same way).
/// </summary>
public sealed class @this : global::app.type.kind.@this
{
    private readonly System.Type? _element;

    /// <summary>The kind for the element type <paramref name="name"/>, whose values are of class
    /// <paramref name="element"/>; null when the element names no class a list can close over.</summary>
    public @this(string name, System.Type? element) : base(name) => _element = element;

    protected internal override string Owner => global::app.type.item.@this.NameOf(typeof(global::app.type.item.list.@this));

    /// <summary>The list's class closed over the element's (<c>list&lt;path&gt;</c>) when the element is an item that
    /// makes itself; else the list's class as it is.</summary>
    public override System.Type? Of(System.Type? type)
        => type == typeof(global::app.type.item.list.@this) && _element != null
           && typeof(global::app.type.item.@this).IsAssignableFrom(_element)
           && typeof(global::app.type.item.ICreate<>).MakeGenericType(_element).IsAssignableFrom(_element)
            ? typeof(global::app.type.item.list.@this<>).MakeGenericType(_element)
            : type;

    /// <summary>A list of records is shown by one of its element (<c>[{"path": …, "verbs": […]}]</c>); a list of any
    /// other element shows the list's own.</summary>
    public override string? Example => IsRecord && Fact("Example") is { } one ? $"[{one}]" : null;

    /// <summary>A list of records says what each element is; a list of any other element says the list's own.</summary>
    public override string? Description => IsRecord && Fact("Description") is { } each ? $"A list of {Name}: {each}" : null;

    // The element is a record — its class declares the object shape.
    private bool IsRecord => Fact("Shape") == "object";

    // A fact the element's class declares (a static Example, Description, Shape); null when it declares none.
    private string? Fact(string member)
        => _element?.GetProperty(member, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
                                         | System.Reflection.BindingFlags.FlattenHierarchy)?.GetValue(null) as string;
}
