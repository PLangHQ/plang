namespace App.modules;

/// <summary>
/// Action modifier: wraps another action's execution via a delegate.
/// The runtime folds modifiers right-to-left — first in the list = outermost wrapper.
/// </summary>
public interface IModifier
{
    Func<Task<Data.@this>> Wrap(Func<Task<Data.@this>> next, Actor.Context.@this context);
}
