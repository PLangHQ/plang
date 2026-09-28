namespace app.module;

/// <summary>
/// An action that runs the actions after it in its step once for each item of a collection — <c>loop.foreach</c>.
/// Its module mints its catalog element as a loop (<see cref="global::app.goal.step.action.loop.@this"/>): the
/// action that leads its step's code, since what follows it is its body.
/// </summary>
public interface ILoop;
