namespace app.module;

/// <summary>
/// Action modifier: wraps another action's execution via a delegate.
/// The runtime folds modifiers right-to-left — first in the list = outermost wrapper.
/// </summary>
public interface IModifier
{
    Func<Task<data.@this>> Wrap(Func<Task<data.@this>> next, actor.context.@this context);
}

/// <summary>
/// An on-error clause: a modifier that handles a failed result when its filters match. The clauses
/// written one after another on one action are ONE try/catch, checked in the order written: the first
/// whose filters match handles the error, the others never see it, and what it returns — a retry's
/// result, its recovery's, or a throw from that recovery — leaves the step.
/// </summary>
public interface ICatch : IModifier
{
    /// <summary>Handles <paramref name="failed"/> when this clause's filters match; null when they
    /// don't, so the next clause gets its turn. <paramref name="attempt"/> re-runs the guarded action.</summary>
    Task<data.@this?> Catch(data.@this failed, Func<Task<data.@this>> attempt, actor.context.@this context);
}
