namespace app.goal.step.line;

/// <summary>
/// Where a step is written in its .goal file: the line it starts on and how deep it is indented (a
/// level is 4 spaces). A step made from a condition's inline <c>{ }</c> body is written on its parent's
/// line and carries it. One object, so what else is learned about a line has a place: the .pr writes
/// it as <c>line: {number, indent?}</c> (indent only when there is one).
/// </summary>
public sealed class @this
{
    [global::app.Debug] public int Number { get; init; }

    [global::app.Debug] public int Indent { get; init; }

    /// <summary>Writes itself as the .pr's <c>line</c> object.</summary>
    public void Output(global::app.type.format.IWriter writer)
    {
        writer.BeginObject();
        writer.Name("number"); writer.Int(Number);
        if (Indent > 0) { writer.Name("indent"); writer.Int(Indent); }
        writer.EndObject();
    }
}
