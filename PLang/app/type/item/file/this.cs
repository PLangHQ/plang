namespace app.type.item.file;

/// <summary>
/// PLang <c>file</c> value — a REFERENCE: a location plus lazy content and
/// metadata. <c>read X</c> yields one with <b>nothing read</b>; the content
/// materialises (and the holding <c>Data</c> narrows to the content's type) on
/// first examination through the value door. The location/stat surface
/// (<c>!file!path</c>, <c>!file!size</c>) never triggers a content read.
///
/// <para>Substitutability: a file is-a path (the location facet), declared via
/// the static-<c>Type</c> lattice convention — same shape as <c>image</c>.
/// The scheme know-how stays on the composed <c>Path</c>
/// (<c>FilePath</c>/<c>HttpPath</c>); the reference owns content laziness.</para>
/// </summary>
[global::app.Attributes.PlangType("file")]
public sealed class @this : global::app.type.item.content.@this, global::app.type.item.ICreate<@this>
{
    public static string Example => "/config/settings.json";
    public static string Description => "A file, by its path; its content is read when it is used.";
    public static string Shape => "string";
    /// <summary>A file is made from a path.</summary>
    public static bool Takes(global::app.type.@this other) => other.Is("path");

    /// <summary>A file reference at <paramref name="path"/>, born with <paramref name="template"/>.</summary>
    public @this(global::app.type.item.path.@this path, global::app.actor.context.@this context, string? template = null)
        : base(path, context, template) { }

    /// <summary>A file is made from its path, as <paramref name="declared"/> says: the reference to what is there,
    /// nothing read, born with the declaration's template. Anything else declines.</summary>
    public static @this? Create(object? raw, global::app.type.@this? declared, global::app.data.@this data) => raw switch
    {
        @this self => self,
        global::app.type.item.path.@this path => new @this(path, data.Context, (declared ?? data.Type).Template),
        _ => null,
    };

    /// <summary>
    /// Navigation is first-touch: a file is a reference to content, so it narrows itself
    /// (read + decode by mime — json→its json host) via the Data door, which caches the decoded value
    /// back onto <paramref name="parent"/> (the file Data BECOMES its content in place, read-once),
    /// then navigates the parsed value. A read/parse failure surfaces the parent's error.
    /// </summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Get(
        global::app.data.@this parent, string key)
    {
        var materialized = await parent.Value();
        if (!parent.Success) return parent;
        return await materialized.Get(parent, key);
    }

    /// <summary>Truthiness of a reference is its location's: does it exist.</summary>
    public override bool IsTruthy() => Path is global::app.type.item.path.file.@this fp && fp.Exists;

    /// <summary>Stat byte-size — the file's `!size` (<c>number</c>); never reads content.</summary>
    public global::app.type.item.number.@this Size =>
        Path is global::app.type.item.path.file.@this fp ? fp.Size : (global::app.type.item.number.@this)0;
}
