namespace app.type.item.property;

/// <summary>
/// A thing a program adds properties to — one that lives on (the app, an actor, a call, a goal, a step, a module). Its
/// <see cref="property"/> node holds what was added; a dot on the thing that names none of its own members falls through
/// to it (<c>%!app.home%</c>), and a write of a name it doesn't list lands there.
/// </summary>
public interface IOwner
{
    /// <summary>What a program added to this thing (<c>%!app.property["home"]%</c>).</summary>
    list.@this property { get; }
}
