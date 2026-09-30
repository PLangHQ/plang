namespace app.module.file;

/// <summary>
/// <c>read X</c> lands a reference to what is at X — a <c>file</c>, a <c>url</c> or a <c>directory</c> —
/// with nothing read; the content is that reference's own value, read at first touch. The path knows what
/// reading it lands.
/// </summary>
[Action("read")]
public partial class Read : IContext
{
    [IsNotNull]
    public partial data.@this<path> Path { get; init; }

    [Default(false)]
    public partial data.@this<global::app.type.item.@bool.@this> ResolveVariables { get; init; }

    public Task<data.@this> Start() => Path.Use(path => ResolveVariables.Use(resolve => path.Read(Context, resolve)));

    /// <summary>A literal path's reference type, for the step that captures it — and a warning when it isn't
    /// there now; a path holding a variable is known only at run.</summary>
    public async Task<data.@this> Build() => Path.HasVariable ? Context.Ok() : await Path.Use(async path =>
    {
        if (await path.Absence(Context) is { } why) await __action.Warn(why, Context);
        return await path.Expect(Context);
    });
}
