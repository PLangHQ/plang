namespace app.type.directory;

/// <summary>
/// PLang <c>directory</c> value — a location plus its lazy listing. TERMINAL:
/// its content type is known up-front (<c>list&lt;path&gt;</c>), so it never
/// narrows. The listing holds the children's LOCATIONS, not content-bearing
/// files — <c>read</c> a child to get content, and a write-out of a directory
/// is a flat listing, never a content dump.
/// </summary>
public sealed class @this : global::app.type.item.@this, module.IContext
{
    public static string Example => "/docs";
    public static string Shape => "string";

    /// <summary>The is-a lattice — a directory is-a path.</summary>
    public static System.Collections.Generic.IReadOnlyList<System.Type> Type { get; }
        = new[] { typeof(@this), typeof(global::app.type.path.@this) };

    /// <summary>The location facet.</summary>
    [global::app.LlmBuilder, global::app.Out, global::app.Store]
    public global::app.type.path.@this Path { get; }

    private global::app.type.list.@this<global::app.type.path.@this>? _list;

    [System.Text.Json.Serialization.JsonIgnore]
    public actor.context.@this? Context
    {
        get => Path.Context;
        set => Path.Context = value;
    }

    public @this(global::app.type.path.@this path)
    {
        Path = path ?? throw new System.ArgumentNullException(nameof(path));
    }

    /// <summary>
    /// The children's locations as a native <c>list</c> of <c>path</c> values,
    /// listed through the path's auth gate on first access and cached.
    /// </summary>
    public async System.Threading.Tasks.Task<global::app.type.list.@this<global::app.type.path.@this>> List()
    {
        if (_list != null) return _list;
        var listed = await Path.List();
        if (!listed.Success)
            throw new System.IO.IOException(listed.Error!.Message);
        return _list = (await listed.Value())!;
    }

    /// <summary>The already-materialised listing, or null when nothing listed yet —
    /// the sync view the renderer reads below the serializer's converter wall.</summary>
    public global::app.type.list.@this<global::app.type.path.@this>? Listed => _list;

    /// <summary>Truthiness — does the directory exist.</summary>
    public override bool IsTruthy() =>
        Path is global::app.type.path.file.@this fp && fp.Exists;

    public override string ToString() => Path.ToString();
}
