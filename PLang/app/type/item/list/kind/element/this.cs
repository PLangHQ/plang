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
    public override global::app.type.item.prose.@this? Example(global::app.actor.context.@this context)
        => _element.IsRecord ? new("[", _element.Example(context), "]") : null;

    /// <summary>A list is offered what its element is (a list of permissions: that the step gives them).</summary>
    public override System.Threading.Tasks.ValueTask<System.Collections.Generic.IReadOnlyList<global::app.type.item.@this>> Offers(global::app.goal.step.@this step)
        => _element.Offers(step);

    /// <summary>A list of records says what each element is; a list of any other element says the list's own.</summary>
    public override global::app.type.item.prose.@this? Description(global::app.actor.context.@this context)
        => _element.IsRecord ? new($"A list of {Name}: ", _element.Description(context)) : null;
}
