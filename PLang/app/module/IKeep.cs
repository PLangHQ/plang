namespace app.module;

/// <summary>
/// An action that keeps a value in a variable — <c>variable.set</c>. Its module mints its catalog element as a
/// keep (<see cref="global::app.goal.step.action.keep.@this"/>): in a step whose other actions produce a value
/// (<c>set %x% = %x% + 1</c>), it keeps that value, so it follows them.
/// </summary>
public interface IKeep;
