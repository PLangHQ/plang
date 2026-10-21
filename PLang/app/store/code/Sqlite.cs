namespace app.store.code;

/// <summary>The built-in store provider: a sqlite database at the file, or in memory under test.</summary>
public sealed class Sqlite : IStore
{
    public string Name => "sqlite";
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    public global::app.store.@this Open(global::app.type.item.path.@this file, System.Func<string?> memory, global::app.actor.context.@this context)
        => new global::app.store.sqlite.@this(file, memory, context);
}
