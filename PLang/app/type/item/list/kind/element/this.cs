namespace app.type.item.list.kind.element;

/// <summary>
/// A kind of list named by its element's type — <c>list&lt;path&gt;</c> is <c>{list, path}</c>. It holds the
/// element's type, and a list of it is <c>list&lt;T&gt;</c> closed over that type's class (a choice set closes
/// <c>choice&lt;T&gt;</c> the same way).
/// </summary>
public sealed class @this : global::app.type.kind.@this
{
    private readonly global::app.type.@this _element;

    /// <summary>The kind for lists of <paramref name="element"/>.</summary>
    public @this(global::app.type.@this element) : base(element.Name) => _element = element;

    protected internal override string Owner => global::app.type.item.@this.NameOf(typeof(global::app.type.item.list.@this));

    /// <summary>The list's class closed over the element's (<c>list&lt;path&gt;</c>) when the element is an item that
    /// makes itself; else the list's class as it is.</summary>
    public override System.Type? Of(System.Type? type)
        => type == typeof(global::app.type.item.list.@this) && _element.ClrType is { } element
           && typeof(global::app.type.item.@this).IsAssignableFrom(element)
           && typeof(global::app.type.item.ICreate<>).MakeGenericType(element).IsAssignableFrom(element)
            ? typeof(global::app.type.item.list.@this<>).MakeGenericType(element)
            : type;

    /// <summary>A list of records is shown by one of its element (<c>[{"path": …, "verbs": […]}]</c>); a list of any
    /// other element shows the list's own — a scalar's example is no formal value to wrap.</summary>
    public override string? Example => _element.IsRecord && _element.Example is { } one ? $"[{one}]" : null;

    /// <summary>A list of records says what each element is; a list of any other element says the list's own.</summary>
    public override string? Description
        => _element.IsRecord && _element.Description is { } each ? $"A list of {Name}: {each}" : null;
}
