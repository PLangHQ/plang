namespace app.type.item.binary;

/// <summary>
/// PLang <c>binary</c> value — raw bytes as a first-class value (file/HTTP byte
/// reads, crypto output). Sibling to <c>image</c> but untyped (no MIME). Backed by
/// a CLR <c>byte[]</c>; the bare wire form is base64.
/// </summary>
[global::app.Attributes.PlangType("binary")]
[global::app.Attributes.Format("", "application/octet-stream", ".bin")]
[global::app.Attributes.Format("exe")]
[global::app.Attributes.Format("dll")]
// video
[global::app.Attributes.Format("mp4", "video/mp4", Compressible = false)]
[global::app.Attributes.Format("webm", "video/webm", Compressible = false)]
[global::app.Attributes.Format("mkv", "video/x-matroska", Compressible = false)]
[global::app.Attributes.Format("mov", "video/quicktime", Compressible = false)]
[global::app.Attributes.Format("avi", "video/x-msvideo", Compressible = false)]
[global::app.Attributes.Format("flv", "video/x-flv", Compressible = false)]
// audio
[global::app.Attributes.Format("mp3", "audio/mpeg", Compressible = false)]
[global::app.Attributes.Format("wav", "audio/wav", Compressible = false)]
[global::app.Attributes.Format("flac", "audio/flac", Compressible = false)]
[global::app.Attributes.Format("aac", "audio/aac", Compressible = false)]
[global::app.Attributes.Format("ogg", "audio/ogg", Compressible = false)]
[global::app.Attributes.Format("m4a", "audio/mp4", Compressible = false)]
// archives — the archive type reads none of them yet
[global::app.Attributes.Format("zip", "application/zip", Compressible = false)]
[global::app.Attributes.Format("rar", "application/vnd.rar", Compressible = false)]
[global::app.Attributes.Format("7z", "application/x-7z-compressed", Compressible = false)]
[global::app.Attributes.Format("tar", "application/x-tar", Compressible = false)]
[global::app.Attributes.Format("gz", "application/gzip", Compressible = false)]
[global::app.Attributes.Format("bz2", "application/x-bzip2", Compressible = false)]
// spreadsheets — table reads only csv yet
[global::app.Attributes.Format("xls", "application/vnd.ms-excel")]
[global::app.Attributes.Format("xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
[global::app.Attributes.Format("ods", "application/vnd.oasis.opendocument.spreadsheet")]
[global::app.Attributes.Format("numbers")]
[global::app.Attributes.Format("gsheet")]
// documents
[global::app.Attributes.Format("doc", "application/msword")]
[global::app.Attributes.Format("docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
[global::app.Attributes.Format("odt", "application/vnd.oasis.opendocument.text")]
[global::app.Attributes.Format("pages")]
[global::app.Attributes.Format("gdoc")]
[global::app.Attributes.Format("pdf", "application/pdf")]
// presentations
[global::app.Attributes.Format("ppt", "application/vnd.ms-powerpoint")]
[global::app.Attributes.Format("pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation")]
[global::app.Attributes.Format("odp", "application/vnd.oasis.opendocument.presentation")]
[global::app.Attributes.Format("gslides")]
// vector, 3d models
[global::app.Attributes.Format("ai")]
[global::app.Attributes.Format("eps")]
[global::app.Attributes.Format("obj")]
[global::app.Attributes.Format("fbx")]
[global::app.Attributes.Format("stl")]
[global::app.Attributes.Format("gltf")]
[global::app.Attributes.Format("glb")]
// databases, data files
[global::app.Attributes.Format("db")]
[global::app.Attributes.Format("sqlite")]
[global::app.Attributes.Format("mdb")]
[global::app.Attributes.Format("sql")]
[global::app.Attributes.Format("parquet")]
[global::app.Attributes.Format("orc")]
[global::app.Attributes.Format("avro")]
[global::app.Attributes.Format("h5")]
[global::app.Attributes.Format("feather")]
[global::app.Attributes.Format("arrow")]
// subtitles, ebooks, fonts
[global::app.Attributes.Format("srt")]
[global::app.Attributes.Format("vtt")]
[global::app.Attributes.Format("sub")]
[global::app.Attributes.Format("epub", "application/epub+zip")]
[global::app.Attributes.Format("mobi")]
[global::app.Attributes.Format("azw3")]
[global::app.Attributes.Format("ttf", "font/ttf")]
[global::app.Attributes.Format("otf", "font/otf")]
[global::app.Attributes.Format("woff", "font/woff")]
[global::app.Attributes.Format("woff2", "font/woff2")]
// packages, disk images, mobile apps
[global::app.Attributes.Format("msi")]
[global::app.Attributes.Format("deb")]
[global::app.Attributes.Format("rpm")]
[global::app.Attributes.Format("pkg")]
[global::app.Attributes.Format("dmg")]
[global::app.Attributes.Format("nupkg")]
[global::app.Attributes.Format("iso")]
[global::app.Attributes.Format("img")]
[global::app.Attributes.Format("vhd")]
[global::app.Attributes.Format("vmdk")]
[global::app.Attributes.Format("qcow2")]
[global::app.Attributes.Format("ova")]
[global::app.Attributes.Format("apk")]
[global::app.Attributes.Format("aab")]
[global::app.Attributes.Format("ipa")]
[global::app.Attributes.Format("xapk")]
// certificates, config, logs
[global::app.Attributes.Format("crt")]
[global::app.Attributes.Format("cer")]
[global::app.Attributes.Format("pem")]
[global::app.Attributes.Format("der")]
[global::app.Attributes.Format("p12")]
[global::app.Attributes.Format("pfx")]
[global::app.Attributes.Format("key")]
[global::app.Attributes.Format("conf")]
[global::app.Attributes.Format("cfg")]
[global::app.Attributes.Format("toml")]
[global::app.Attributes.Format("properties")]
[global::app.Attributes.Format("env")]
[global::app.Attributes.Format("log")]
// machine learning, email, calendar, gis, checksums
[global::app.Attributes.Format("pt")]
[global::app.Attributes.Format("pth")]
[global::app.Attributes.Format("pb")]
[global::app.Attributes.Format("onnx")]
[global::app.Attributes.Format("joblib")]
[global::app.Attributes.Format("eml")]
[global::app.Attributes.Format("msg")]
[global::app.Attributes.Format("ics", "text/calendar")]
[global::app.Attributes.Format("shp")]
[global::app.Attributes.Format("geojson", "application/geo+json")]
[global::app.Attributes.Format("kml", "application/vnd.google-earth.kml+xml")]
[global::app.Attributes.Format("gpx")]
[global::app.Attributes.Format("sha256")]
[global::app.Attributes.Format("md5")]
[global::app.Attributes.Format("sfv")]
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>,
    global::app.type.item.IEncode<@this>
{
    /// <summary>Binary's formats written: a binary value is its bytes. Any other value is no content of these
    /// formats — an error, never a guessed writer.</summary>
    public static async System.Threading.Tasks.Task<global::app.data.@this> Encode(System.IO.Stream stream,
        global::app.data.@this data, global::app.actor.context.@this context, global::app.View? view,
        System.Text.Encoding? encoding, System.Threading.CancellationToken ct)
    {
        if (await data.Value() is not @this bytes)
            return context.Error(new global::app.error.Error(
                $"%{data.Name}% holds a {data.Type?.Name ?? "value"}, not bytes — nothing writes it as binary content", "NoEncoder", 400));
        await stream.WriteAsync(bytes.Value, ct);
        await stream.FlushAsync(ct);
        return context.Ok();
    }

    public static string Description => "Bytes. Its kind is what they hold, when known (png, pdf, json, …).";
    public static IReadOnlyList<string> Alias { get; } = ["bytes"];
    public static string Shape => "string";

    public byte[] Value { get; }

    /// <summary>The value's kind — the byte-format vocabulary. An ordinary
    /// typed property stamped at creation, never after.</summary>
    public string? Kind { get; init; }

    protected internal override global::app.type.@this Type
        => new("binary", typeof(@this), Kind);

    public @this(byte[] value) { Value = value ?? System.Array.Empty<byte>(); }

    /// <summary>THE PURE CORE — a <c>binary</c> passes through; a raw <c>byte[]</c> passes; a base64
    /// string decodes; anything else (or non-base64) declines (<c>null</c>). Shared by the ICreate
    /// courier and comparison coercion.</summary>
    public static @this? Create(object? raw)
    {
        if (raw is @this self) return self;
        object? value = raw is global::app.type.item.@this rit ? rit.Clr<object>() : raw;
        switch (value)
        {
            case byte[] b: return (@this)b;
            case string s:
                try { return (@this)System.Convert.FromBase64String(s); }
                catch (System.FormatException) { return null; }
            default: return null;
        }
    }

    /// <summary>The ICreate courier face — delegates to the pure core; on decline lands the reason
    /// on <paramref name="data"/> (a non-base64 string vs a wrong type).</summary>
    public static @this? Create(object? value, global::app.data.@this data)
    {
        if (Create(value) is { } built) return built;
        object? clr = (value as global::app.type.item.@this)?.Clr<object>() ?? value;
        data.Fail(clr is string
            ? new global::app.error.Error("Cannot parse string as binary — expected base64.", "BinaryParseFailed", 400)
            : new global::app.error.Error($"Cannot convert {(value as global::app.type.item.@this)?.Type.Name ?? value?.GetType().Name} to binary.", "BinaryConversionFailed", 400));
        return null;
    }

    /// <summary>A re-kinded copy — same bytes, the declared kind stamped.</summary>
    public override global::app.type.item.@this Kinded(string? kind) => new @this(Value) { Kind = kind };

    // INBOUND only — the entry lift (`.Ok(bytes)` constructs). The outbound
    // implicit (binary → byte[]) is gone: every site was a silent CLR exit;
    // a reader names the bytes face (`.Value`) at a real .NET edge. byte[] is
    // a reference type, so only @this==@this is defined (a byte[] overload
    // would make `binary == null` ambiguous).
    public static implicit operator @this(byte[] v) => new(v);

    /// <summary>The CLR exit door — binary hands its own bytes.</summary>
    internal override object? Clr(System.Type target) => ClrConvert(Value, target);

    public override bool IsLeaf => true;
    public override void Write(global::app.type.format.IWriter w) => w.Bytes(Value);

    /// <summary>binary's byte face IS its bytes.</summary>
    public override byte[]? RawBytes => Value;

    /// <summary>Non-empty bytes are truthy.</summary>
    public override bool IsTruthy() => Value.Length > 0;

    /// <summary>Bare base64 — the serializer renders this.</summary>
    public override string ToString() => System.Convert.ToBase64String(Value);

    // ---- Comparison — the value's own behavior (see app.data.Comparison) ----

    /// <summary>Outranks text — bytes never compare lexically.</summary>
    public override int Rank => 250;

    /// <summary>Equality-only: same byte sequence → <c>Equal</c>, else <c>NotEqual</c>;
    /// a side that can't become bytes → <c>Incomparable</c>. No order.</summary>
    protected override System.Threading.Tasks.ValueTask<global::app.data.Comparison> Order(global::app.type.item.@this other, global::app.actor.context.@this context)
    {
        var b = other as @this ?? Create(other);
        return new(b is null ? global::app.data.Comparison.Incomparable
                 : Value.AsSpan().SequenceEqual(b.Value) ? global::app.data.Comparison.Equal
                 : global::app.data.Comparison.NotEqual);
    }

    public bool AreEqual(object? other) => other switch
    {
        @this b => Value.AsSpan().SequenceEqual(b.Value),
        byte[] arr => Value.AsSpan().SequenceEqual(arr),
        _ => false,
    };

    public override bool Equals(object? obj) => AreEqual(obj);
    public override int GetHashCode()
    {
        var hash = new System.HashCode();
        hash.AddBytes(Value);
        return hash.ToHashCode();
    }
}
