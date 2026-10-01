namespace app.type.item.directory;

/// <summary>
/// PLang <c>directory</c> value — a location plus its lazy listing. TERMINAL:
/// its content type is known up-front (<c>list&lt;path&gt;</c>), so it never
/// narrows. The listing holds the children's LOCATIONS, not content-bearing
/// files — <c>read</c> a child to get content, and a write-out of a directory
/// is a flat listing, never a content dump.
/// </summary>
[global::app.Attributes.PlangType("directory")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    public static string Example => "/docs";
    public static string Description => "A folder, by its path.";
    public static string Shape => "string";
    /// <summary>A directory is made from a path.</summary>
    public static bool Takes(global::app.type.@this other) => other.Is("path");

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
        // Born from a path — its type in this value's history, so `is path` answers from the chain.
        history.Add(path);
    }

    /// <summary>A directory is made from its path; anything else declines.</summary>
    public static @this? Create(object? raw) => raw switch
    {
        @this self => self,
        global::app.type.item.path.@this path => new @this(path),
        _ => null,
    };

    /// <summary>
    /// The children's locations as a native <c>list</c> of <c>path</c> values,
    /// listed through the path's auth gate (as the caller) on first access and cached;
    /// a directory that can't be listed answers why (the path verb's own error).
    /// </summary>
    public async System.Threading.Tasks.Task<global::app.data.@this<global::app.type.item.list.@this<global::app.type.item.path.@this>>> List(actor.context.@this context)
    {
        if (_list != null) return context.Ok<global::app.type.item.list.@this<global::app.type.item.path.@this>>(_list);
        var listed = await Path.List(context);
        if (listed.Success) _list = (await listed.Value())!;
        return listed;
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
        var listed = await List(data.Context!);
        if (listed.Success) return this;
        data.Fail(listed.Error!);
        return Absent;
    }

    /// <summary>The item membership hook — routes to the listing rule below.</summary>
    public override async System.Threading.Tasks.ValueTask<bool> Contains(global::app.data.@this needle)
        => await Contains(needle.ToString() ?? "", needle.Context);

    /// <summary>Membership is over the LISTING's locations (a directory "contains"
    /// a name when some child's location carries it) — never over content.</summary>
    public async System.Threading.Tasks.Task<bool> Contains(string needle, actor.context.@this context)
    {
        if (string.IsNullOrEmpty(needle)) return false;
        // membership answers a bool, so a directory that can't be listed says why as a thrown program error
        var listed = await List(context);
        if (!listed.Success) throw new global::app.error.AppException(listed.Error!);
        foreach (var slot in (await listed.Value())!.Slots())
            if ((slot is global::app.data.@this d ? d.Peek() : slot)?.ToString()?.Contains(needle, System.StringComparison.OrdinalIgnoreCase) == true)
                return true;
        return false;
    }

    /// <summary>Truthiness — does the directory exist, as its asker may see.</summary>
    public override System.Threading.Tasks.Task<bool> AsBooleanAsync(global::app.actor.context.@this context)
        => Path.AsBooleanAsync(context);

    public override string ToString() => Path.ToString();

    /// <summary>A directory writes its own form (<see cref="Write"/>) in every view; it never lists while it
    /// writes.</summary>
    public override System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
    {
        Write(writer);
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }

    /// <summary>Opened to be written out: the directory lists its children (as the writer, past the path's auth
    /// gate).</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.error.Error?> Open(global::app.actor.context.@this context)
    {
        var listed = await List(context);
        return listed.Success ? null : listed.Error;
    }

    /// <summary>
    /// The directory renders itself as a FLAT LISTING of its children's
    /// locations, never their contents. An unlisted directory renders its
    /// location (the reference face).
    /// </summary>
    public override void Write(global::app.type.format.IWriter writer)
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
