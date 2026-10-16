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

    /// <summary>The type the content is read as — the step's <c>as &lt;type&gt;</c> force. None given, the location's
    /// extension decides the reference's kind (<c>notes.md</c> → <c>{file, md}</c>); given, the content is born as
    /// that type through its own birth (<c>as text</c> → the characters, a plain <c>text</c>), beating the extension.</summary>
    public partial data.@this<global::app.type.@this>? Type { get; init; }

    // No Type given: a Template is opened once and its kind handed to the read, else the read lands a reference the
    // extension types. A Type given reads the bytes now and the declared type makes itself from them through its own
    // birth (as text → the characters), the file its origin — so the value landed is that type, not a file reference.
    public async Task<data.@this> Start()
    {
        var typed = Type is not null && await Type.ToBooleanAsync();
        return await Path.Use(async path => typed
            ? await Type!.Use(async declared =>
            {
                var bytes = await path.Bytes(Context);
                return bytes.Success
                    ? await declared.Create((await bytes.Value())?.Value, Context, path.Name.ToString(), path)
                    : bytes;
            })
            : Template is { IsInitialized: true }
                ? await Template.Use(kind => path.Read(Context, kind.Value))
                : await path.Read(Context));
    }

    /// <summary>What a step capturing this read takes as its type — and a warning when the path isn't there now; a
    /// path holding a variable is known only at run. With an <c>as &lt;type&gt;</c>, the read lands THAT type, so the
    /// capture takes it (not the path's reference type); the path is still read, so the absence warning still means
    /// something.</summary>
    public async Task<data.@this> Build() => Path.HasVariable ? Context.Ok() : await Path.Use(async path =>
    {
        if (await path.Absence(Context) is { } why) await __action.Warn(why, Context);
        return Type is not null && await Type.ToBooleanAsync() ? Context.Ok(await Type.Value()) : await path.Expect(Context);
    });
}
