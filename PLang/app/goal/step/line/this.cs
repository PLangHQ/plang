namespace app.goal.step.line;

/// <summary>
/// Where a step is written in its .goal file: the line it starts on and how deep it is indented (a
/// level is 4 spaces). A step made from a condition's inline <c>{ }</c> body is written on its parent's
/// line and carries it. One object, so what else is learned about a line has a place: the .pr writes
/// it as <c>line: {number, indent}</c>, by its tags.
/// </summary>
public sealed class @this
{
    [Store, Out, global::app.Debug] public int Number { get; init; }

    [Store, Out, global::app.Debug] public int Indent { get; init; }
}
