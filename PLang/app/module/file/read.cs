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

    /// <summary>The kind of template the content is ("load vars": plang — its %variables% filled from memory); none,
    /// it is read as written.</summary>
    public partial data.@this<global::app.type.item.choice.@this<global::app.type.item.template.kind.@this>>? Template { get; init; }

    // none given reads as written; one given is opened once and its kind handed to the read — a name that is none of
    // the kinds is the choice's own refusal
    public Task<data.@this> Start() => Path.Use(path => Template is not { IsInitialized: true }
        ? path.Read(Context)
        : Template.Use(kind => path.Read(Context, kind.Value)));

    /// <summary>A literal path's reference type, for the step that captures it — and a warning when it isn't
    /// there now; a path holding a variable is known only at run.</summary>
    public async Task<data.@this> Build() => Path.HasVariable ? Context.Ok() : await Path.Use(async path =>
    {
        if (await path.Absence(Context) is { } why) await __action.Warn(why, Context);
        return await path.Expect(Context);
    });
}
