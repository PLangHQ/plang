namespace app.type.item.list.kind.element;

/// <summary>
/// A kind of list named by its element's type — <c>list&lt;path&gt;</c> is <c>{list, path}</c>. It holds the
/// element's type, and a list of it is <c>list&lt;T&gt;</c> closed over that type's class (a choice set closes
/// <c>choice&lt;T&gt;</c> the same way).
/// </summary>
public sealed class @this : global::app.type.kind.@this<global::app.type.item.list.@this>
{
    private readonly global::app.type.@this _element;

    /// <summary>The kind for lists of <paramref name="element"/> — named by the element's type. When the element is
    /// itself kinded (a list of lists: <c>{list, kind: {list, kind: path}}</c>), that kind rides as this kind's own,
    /// so the nesting is kept, not flattened.</summary>
    public @this(global::app.type.@this element) : base(element.Name, element.kind.IsEmpty ? null : element.kind) => _element = element;

    /// <summary>The kind's class is its element's — a <c>list&lt;action&gt;</c> kind IS the registered action
    /// type at its kind, so it carries action's ClrType, not null.</summary>
    internal override System.Type? ClrType => _element.ClrType;

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
    protected override global::app.type.item.prose.@this? OwnExample(global::app.actor.context.@this context)
        => _element.IsRecord ? new("[", _element.Example(context), "]") : null;

    /// <summary>A list is offered what its element is (a list of permissions: that the step gives them).</summary>
    public override System.Threading.Tasks.ValueTask<System.Collections.Generic.IReadOnlyList<global::app.type.item.@this>> Offers(global::app.goal.step.@this step)
        => _element.Offers(step);

    /// <summary>A list of records says what each element is; a list of any other element says the list's own.</summary>
    protected override global::app.type.item.prose.@this? OwnDescription(global::app.actor.context.@this context)
        => _element.IsRecord ? new($"A list of {Name}: ", _element.Description(context)) : null;
}
