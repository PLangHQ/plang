using Archive = global::app.module.archive.type.archive.@this;
using File = global::app.type.item.path.file.@this;
using Format = global::app.module.archive.type.archive.kind.@this;

namespace app.module.archive.code;

/// <summary>
/// The archive module's provider: it finds the format and hands it the stream — the format packs and unpacks itself,
/// the value says what it packs as, the archive says what it holds.
/// </summary>
public class Default : IArchive
{
    public string Name => "default";
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    public async Task<data.@this> Pack(pack action)
    {
        var context = action.Context;
        var value = await action.Value.Value();
        if (!action.Value.Success) return action.Value;
        var to = action.To == null || !action.To.IsInitialized ? null : await action.To.Value();
        if (action.To != null && action.To.IsInitialized && !action.To.Success) return action.To;
        var setting = context.Setting.Of<global::app.module.archive.setting.@this>();
        var level = action.Level == null || !action.Level.IsInitialized ? setting.Level.Value : (await action.Level.Value())!.Value;

        // the format named, else the one the archive's path ends in, else gzip
        var format = action.Format == null || !action.Format.IsInitialized ? null : (await action.Format.Value())?.Value;
        if (action.Format != null && action.Format.IsInitialized && !action.Format.Success) return action.Format;
        format ??= (to is File at ? new Archive(at, null, context).Format : null)
                   ?? (Format)context.App.type.list["archive"].kind["gzip"]!;

        if (to == null)
        {
            using var packed = new MemoryStream();
            var (result, held) = await format.Pack(action.Value, value!, packed, level, context);
            return result.Success ? context.Ok(new Archive(packed.ToArray(), format, held!)) : result;
        }

        // written to the path as it is packed, never held whole; a pack that fails leaves nothing there
        var pipe = new System.IO.Pipelines.Pipe();
        var writing = Write(to, pipe, context);
        (data.@this result, global::app.module.archive.type.archive.held.@this? held) made;
        await using (var into = pipe.Writer.AsStream())
            made = await format.Pack(action.Value, value!, into, level, context);
        var written = await writing;
        if (made.result.Success && written.Success) return context.Ok<global::app.type.item.path.@this>(to);
        await to.Delete(context);
        return made.result.Success ? written : made.result;
    }

    // the pipe's bytes written to the path as they come; once the path has them all — or refused them — the pipe's
    // reading end is done, so the pack never waits on a reader that left
    private async Task<data.@this> Write(global::app.type.item.path.@this to, System.IO.Pipelines.Pipe pipe, global::app.actor.context.@this context)
    {
        try
        {
            await using var from = pipe.Reader.AsStream();
            return await to.Write(from, context);
        }
        finally
        {
            await pipe.Reader.CompleteAsync();
        }
    }

    public async Task<data.@this> Unpack(unpack action)
    {
        var context = action.Context;
        var archive = await action.Value.Value();
        if (!action.Value.Success) return action.Value;
        var into = action.Into == null || !action.Into.IsInitialized ? null : await action.Into.Value();
        if (action.Into != null && action.Into.IsInitialized && !action.Into.Success) return action.Into;
        if (into != null && into is not File)
            return context.Error(new global::app.error.ServiceError($"{into} is no folder on this machine: an archive unpacks into a folder", "UnpackNeedsFolder", 400));
        var max = action.Max == null || !action.Max.IsInitialized ? context.Setting.Of<global::app.module.archive.setting.@this>().Max : (await action.Max.Value())!;
        if (action.Max != null && action.Max.IsInitialized && !action.Max.Success) return action.Max;
        var named = action.Format == null || !action.Format.IsInitialized ? null : (await action.Format.Value())?.Value;
        if (action.Format != null && action.Format.IsInitialized && !action.Format.Success) return action.Format;

        var (stream, refused) = await archive!.Open(context);
        if (stream == null) return refused!;
        await using var start = new peeked(stream);
        // the format named, else the archive's own, else the one its first bytes are
        var format = named ?? archive.Format
            ?? (context.App.type.list["archive"].kind as global::app.type.kind.empty.@this)?.Kinds.OfType<Format>().FirstOrDefault(k => k.Reads(start.Start));
        if (format == null)
            return context.Error(new global::app.error.ServiceError(
                $"no archive format reads {archive.At?.Raw ?? "this archive"} (its first bytes are {Convert.ToHexString(start.Start)}): gzip, tar, tar.gz, zip and brotli are read — name it (as tar.gz) if it is one of them",
                "UnpackNotSupported", 415));
        return await format.Unpack(start, archive.Held, into as File, max, context);
    }
}
