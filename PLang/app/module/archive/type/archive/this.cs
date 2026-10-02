using File = global::app.type.item.path.file.@this;

namespace app.module.archive.type.archive;

/// <summary>
/// PLang <c>archive</c> value — packed bytes in a format, its kind (<c>archive&lt;gzip&gt;</c>, <c>archive&lt;tar.gz&gt;</c>), and
/// what they hold (<see cref="Held"/>), so unpacking gives that back. An archive is held in memory (what <c>pack</c>
/// answers), or rests in a file (<c>/backup/photos.tar.gz</c>): a path is an archive at rest, read as it streams. Its
/// wire form is <c>{value, held}</c>.
/// </summary>
[global::app.Attributes.PlangType("archive")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    public static string Description => "Packed bytes in a format (gzip, tar.gz, zip…), and what they hold.";
    public static string Shape => "object";

    /// <summary>The packed bytes, when the archive is held in memory.</summary>
    [Out, Store] public global::app.type.item.binary.@this? Value { get; }

    /// <summary>What the archive holds — a Data, a file with its name, a folder. Unknown for an archive at rest that
    /// plang didn't pack.</summary>
    [Out, Store] public held.@this? Held { get; }

    // the file it rests in, when it isn't held in memory
    private readonly File? _at;
    // its format, when known — an archive at rest named by no format and no suffix is known by its first bytes
    private readonly kind.@this? _kind;

    /// <summary>An archive held in memory: <paramref name="value"/>, in <paramref name="kind"/>, holding
    /// <paramref name="held"/>.</summary>
    public @this(byte[] value, kind.@this kind, held.@this held)
    {
        Value = value;
        _kind = kind;
        Held = held;
    }

    /// <summary>An archive resting in the file <paramref name="at"/>, in <paramref name="kind"/> when it is named, else
    /// the format its name ends in — the longest that fits (<c>photos.tar.gz</c> is tar.gz, not gzip); else known by
    /// its first bytes when it is read. It holds a file, named as the archive is without its suffix.</summary>
    public @this(File at, kind.@this? kind, global::app.actor.context.@this context)
    {
        _at = at;
        _kind = kind ?? (context.App.type.list["archive"].kind as global::app.type.kind.empty.@this)?.Kinds.OfType<kind.@this>()
            .Where(k => k.Suffix is { } suffix && at.Raw.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            .MaxBy(k => k.Suffix!.Length);
        var name = at.FileName;
        Held = new held.@this("file", _kind?.Suffix is { } end && name.EndsWith(end, StringComparison.OrdinalIgnoreCase) ? name[..^end.Length] : name);
    }

    /// <summary>Its format, when known.</summary>
    public kind.@this? Format => _kind;

    /// <summary>Where it rests, when it rests in a file.</summary>
    public File? At => _at;

    protected internal override global::app.type.@this Type => new("archive", typeof(@this), _kind?.Name);

    public override bool IsLeaf => false;

    /// <summary>Non-empty bytes, or a file, are truthy.</summary>
    public override bool IsTruthy() => _at != null || Value?.Value.Length > 0;

    /// <summary>The packed bytes, to read as they stream — from memory, or from the file it rests in (gated as a read);
    /// or why they can't be read. The caller disposes the stream.</summary>
    internal async Task<(System.IO.Stream? stream, global::app.data.@this? refused)> Open(global::app.actor.context.@this context)
        => _at != null ? await _at.Open(context) : (new System.IO.MemoryStream(Value?.Value ?? []), null);

    /// <summary>An archive passes through; a file path is an archive resting there, its format the one declared
    /// (<c>as tar.gz</c>), else the one its name ends in; a dict is what an archive wrote of itself
    /// (<c>{value, held}</c>, its format the declared kind).</summary>
    public static @this? Create(object? raw, global::app.type.@this? declared, global::app.data.@this data)
    {
        if (raw is @this archive) return archive;
        var formats = data.Context?.App.type.list["archive"].kind;
        var named = declared?.kind is { IsEmpty: false } asked ? formats?[asked.Name] as kind.@this : null;
        if (raw is global::app.type.item.file.@this { Path: File referenced }) raw = referenced;
        if (raw is global::app.type.item.text.@this written && data.Context is { } reader) raw = global::app.type.item.path.@this.Resolve(written.ToString(), reader);
        if (raw is File at && data.Context is { } asker) return new @this(at, named, asker);
        if (raw is global::app.type.item.dict.@this dict && data.Context is { } context && named != null)
        {
            byte[]? value = null;
            held.@this? holds = null;
            foreach (var entry in dict.Entries(context))
                switch (entry.Name.ToLowerInvariant())
                {
                    case "value": value = (entry.Peek() as global::app.type.item.binary.@this)?.Value
                                          ?? (entry.Peek()?.ToString() is { } text ? System.Convert.FromBase64String(text) : null); break;
                    case "held": holds = held.@this.Create(entry.Peek(), null, data); break;
                }
            if (value != null && holds != null) return new @this(value, named, holds);
        }
        data.Fail(new global::app.error.Error(
            $"%{data.Name}% is no archive: an archive is what pack answers, or a file in a format (gzip, tar.gz, zip…)", "ArchiveInvalid", 400));
        return null;
    }
}
