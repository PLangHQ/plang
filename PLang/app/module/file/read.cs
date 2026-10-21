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

    // The step forced an `as <type>` — a truthy Type slot. Read by Start (what it lands) and Build (what a
    // capturing step takes).
    private async Task<bool> Typed() => Type is not null && await Type.ToBooleanAsync();

    // No Type given: a Template is opened once and its kind handed to the read, else the read lands a reference the
    // extension types. A Type given reads the bytes now and the declared type makes itself from them through its own
    // birth (as text → the characters), the file its origin — so the value landed is that type, not a file reference.
    public async Task<data.@this> Start()
    {
        var typed = await Typed();
        return await Path.Use(async path => typed
            ? await Type!.Use(async type =>
            {
                var bytes = await path.Bytes(Context);
                return bytes.Success
                    ? await type.Create((await bytes.Value())?.Value, Context, path.Name.ToString(), path)
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
    public async Task<data.@this> Build()
    {
        // The `as <type>` is known at build whatever the path holds, so a capturing step takes that type carrier
        // whole, unopened. A literal path also publishes its reference type (no as-type) and is checked for absence
        // now; a variable path is known only at run, so no reference type and no absence check.
        var typed = await Typed();
        return Path.HasVariable
            ? typed ? Type! : Context.Ok()
            : await Path.Use(async path =>
            {
                if (await path.Absence(Context) is { } why) await __action.Warn(why, Context);
                return typed ? Type! : await path.Expect(Context);
            });
    }
}
