using System.IO.Compression;
using File = global::app.type.item.path.file.@this;

namespace app.module.archive.type.archive.kind.compression;

/// <summary>
/// A compression — one value or one file, its bytes compressed (<c>gzip</c>, <c>deflate</c>, <c>brotli</c>). What it
/// packs is the value's to say (<c>item.Pack</c>): a Data whole in plang's own format, a file's contents with its name.
/// Unpacked, a Data comes back as the Data; a file is written into a folder, under its name.
/// </summary>
public abstract class @this : kind.@this
{
    protected @this(string name) : base(name) { }

    /// <summary>A stream that compresses what is written to it into <paramref name="into"/> (left open).</summary>
    protected abstract System.IO.Stream Writer(System.IO.Stream into, CompressionLevel level);

    /// <summary>A stream that reads <paramref name="from"/> uncompressed.</summary>
    protected abstract System.IO.Stream Reader(System.IO.Stream from);

    /// <summary>How hard <paramref name="level"/> asks a compression to work.</summary>
    internal static CompressionLevel Of(global::app.module.archive.setting.Level level) => level switch
    {
        global::app.module.archive.setting.Level.fastest => CompressionLevel.Fastest,
        global::app.module.archive.setting.Level.smallest => CompressionLevel.SmallestSize,
        global::app.module.archive.setting.Level.none => CompressionLevel.NoCompression,
        _ => CompressionLevel.Optimal,
    };

    internal override async Task<(global::app.data.@this result, held.@this? held)> Pack(global::app.data.@this self,
        global::app.type.item.@this value, System.IO.Stream into, global::app.module.archive.setting.Level level,
        global::app.actor.context.@this context)
    {
        (global::app.data.@this result, string held, string? name) packed;
        await using (var writer = Writer(into, Of(level)))
            packed = await value.Pack(self, writer, context);
        return packed.result.Success ? (packed.result, new held.@this(packed.held, packed.name)) : (packed.result, null);
    }

    internal override async Task<global::app.data.@this> Unpack(System.IO.Stream from, held.@this? held, File? into,
        global::app.type.item.size.@this max, global::app.actor.context.@this context)
    {
        held ??= new held.@this("file");
        try
        {
            await using var read = new cap.@this(max).Over(Reader(from));
            if (held.IsData)
            {
                using var whole = new System.IO.MemoryStream();
                await read.CopyToAsync(whole, context.CancellationToken);
                return await context.App.type.list["wire"].kind["plang"]!.Decode(whole.ToArray(), context, ct: context.CancellationToken);
            }
            if (into == null)
                return context.Error(new global::app.error.ServiceError(
                    $"an archived {held.Of} unpacks into a folder: say where (unpack … into /folder)", "UnpackNeedsFolder", 400));
            var (at, refused) = await Under(into, held.Name?.ToString() ?? "unpacked", context);
            if (at == null) return refused ?? context.Error(new global::app.error.ServiceError(
                $"an archived {held.Of} named {held.Name} has no place inside {into.Raw}", "UnpackFailed", 400));
            await Clear(at, context);
            var written = await at.Write(read, context);
            return written.Success ? context.Ok<global::app.type.item.path.@this>(at) : written;
        }
        catch (global::app.error.AppException refused)
        {
            return context.Error(refused.Error);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or FormatException)
        {
            return context.Error(new global::app.error.ServiceError($"Could not unpack {Name}: {ex.Message}", "UnpackFailed", 500));
        }
    }
}
