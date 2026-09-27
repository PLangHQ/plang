namespace app.type.item;

/// <summary>
/// Which one of its kind is in play for the asker (one item): the running goal, the acting actor.
/// A concept nothing is ever inside answers none — the default.
/// </summary>
public interface ICurrent<TSelf> where TSelf : @this, ICurrent<TSelf>
{
    static virtual TSelf? Current(global::app.actor.context.@this context) => null;
}
