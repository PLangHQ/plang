namespace app.module;

/// <summary>
/// Action modifier: wraps another action's execution via a delegate.
/// The runtime folds modifiers right-to-left — first in the list = outermost wrapper.
/// </summary>
public interface IModifier
{
    Func<Task<data.@this>> Wrap(Func<Task<data.@this>> next, actor.context.@this context);
}
