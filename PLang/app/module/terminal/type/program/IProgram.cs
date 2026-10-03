namespace app.module.terminal.type.program;

/// <summary>A step that starts a program (<c>terminal.start</c>, <c>terminal.open</c>): what it names of the program —
/// the options both share — and the terminal that spawns it.</summary>
public interface IProgram
{
    global::app.actor.context.@this Context { get; }
    data.@this<global::app.type.item.text.@this> App { get; }
    data.@this<global::app.type.item.list.@this>? Parameter { get; }
    data.@this<global::app.type.item.dict.@this>? Environment { get; }
    data.@this<global::app.type.item.path.@this>? WorkingDirectory { get; }
    data.@this<global::app.type.item.list.@this<global::app.type.item.permission.@this>>? Permission { get; }
    data.@this<global::app.type.item.@bool.@this> Clean { get; }
    data.@this<global::app.type.item.list.@this>? Keep { get; }
    code.ITerminal Terminal { get; }
}
