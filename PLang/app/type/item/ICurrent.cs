namespace app.type.item;

/// <summary>
/// A concept that execution is inside — one of its kind is in play for the asker: the running goal, the acting
/// actor, the test in progress, the error in play. Its type is a node with a current
/// (<see cref="global::app.type.current.@this{T, L}"/>). A concept nothing is ever inside does not declare it.
/// </summary>
public interface ICurrent<TSelf> where TSelf : @this, ICurrent<TSelf>
{
    static abstract TSelf? Current(global::app.actor.context.@this context);
}
