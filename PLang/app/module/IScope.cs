namespace app.module;

/// <summary>
/// An action that changes what the build knows of a goal's variables. At build, the goal is walked
/// over a scratch store (<c>goal.step.list.Scope</c>): no action runs, and each one leaves its
/// return's empty value as <c>%!data%</c> — except an IScope action, which makes its own change to
/// its context's store (the scratch one) instead: variable.set binds its name, loop.foreach binds its
/// item. What it can't know, it leaves out — an unknown variable is never guessed.
/// </summary>
public interface IScope
{
    System.Threading.Tasks.Task Scope();
}
