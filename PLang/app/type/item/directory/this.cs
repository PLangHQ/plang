namespace app.type.item.directory;

/// <summary>
/// PLang <c>directory</c> value — a location plus its lazy listing. TERMINAL:
/// its content type is known up-front (<c>list&lt;path&gt;</c>), so it never
/// narrows. The listing holds the children's LOCATIONS, not content-bearing
/// files — <c>read</c> a child to get content, and a write-out of a directory
/// is a flat listing, never a content dump.
/// </summary>
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    public static string Example => "/docs";
    public static string Shape => "string";

    /// <summary>The is-a lattice — a directory is-a path.</summary>
    public static new System.Collections.Generic.IReadOnlyList<System.Type> Type { get; }
        = new[] { typeof(@this), typeof(global::app.type.item.path.@this) };

    /// <summary>The location facet.</summary>
    [global::app.LlmBuilder, global::app.Out, global::app.Store]
    public global::app.type.item.path.@this Path { get; }

    private global::app.type.item.list.@this<global::app.type.item.path.@this>? _list;

    public @this(global::app.type.item.path.@this path)
    {
        Path = path ?? throw new System.ArgumentNullException(nameof(path));
    }

    /// <summary>
    /// The children's locations as a native <c>list</c> of <c>path</c> values,
    /// listed through the path's auth gate (as the caller) on first access and cached.
    /// </summary>
    public async System.Threading.Tasks.Task<global::app.type.item.list.@this<global::app.type.item.path.@this>> List(actor.context.@this context)
    {
        if (_list != null) return _list;
        var listed = await Path.List(context);
        if (!listed.Success)
            throw new System.IO.IOException(listed.Error!.Message);
        return _list = (await listed.Value())!;
    }

    /// <summary>The already-materialised listing, or null when nothing listed yet —
    /// the sync view the renderer reads below the serializer's converter wall.</summary>
    public global::app.type.item.list.@this<global::app.type.item.path.@this>? Listed => _list;


    /// <summary>
    /// Materialize door — pull the listing into memory so the sync leaf write emits
    /// the flat listing of child locations. An unlisted directory falls back to its
    /// location (the reference face). Parallel to file/url/image's <c>Value</c>:
    /// <c>.Value()</c> is the uniform materialization for every reference fundamental,
    /// so the serializer needs no separate load pass.
    /// </summary>
    public override async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Value(global::app.data.@this data)
    {
        try { await List(data.Context); }
        catch (System.IO.IOException ex)
        {
            data.Fail(new global::app.error.Error(
                $"could not list '{Path}': {ex.Message}", "DirectoryListFailed", 400) { Exception = ex });
            return Absent;
        }
        return this;
    }

    /// <summary>The item membership hook — routes to the listing rule below.</summary>
    public override async System.Threading.Tasks.ValueTask<bool> Contains(global::app.data.@this needle)
        => await Contains(needle.ToString() ?? "", needle.Context);

    /// <summary>Membership is over the LISTING's locations (a directory "contains"
    /// a name when some child's location carries it) — never over content.</summary>
    public async System.Threading.Tasks.Task<bool> Contains(string needle, actor.context.@this context)
    {
        if (string.IsNullOrEmpty(needle)) return false;
        var listing = await List(context);
        foreach (var slot in listing.Slots())
            if ((slot is global::app.data.@this d ? d.Peek() : slot)?.ToString()?.Contains(needle, System.StringComparison.OrdinalIgnoreCase) == true)
                return true;
        return false;
    }

    /// <summary>Truthiness — does the directory exist.</summary>
    public override bool IsTruthy() =>
        Path is global::app.type.item.path.file.@this fp && fp.Exists;

    public override string ToString() => Path.ToString();

    /// <summary>
    /// The directory renders itself as a FLAT LISTING of its children's
    /// locations, never their contents. An unlisted directory renders its
    /// location (the reference face); the listing was pre-materialised by the
    /// serialize chokepoint's <c>Load()</c> pass.
    /// </summary>
    public override void Write(global::app.channel.serializer.IWriter writer)
    {
        var listed = Listed;
        if (listed == null) { writer.String(ToString()); return; }
        writer.BeginArray(listed.CountRaw);
        foreach (var slot in listed.Slots())
        {
            var entry = slot is global::app.data.@this d ? d.Peek() : slot;
            if (entry is global::app.type.item.path.@this p) p.Write(writer);
            else writer.String(entry?.ToString() ?? "");
        }
        writer.EndArray();
    }
}
