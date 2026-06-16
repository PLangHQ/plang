using System.Collections.Concurrent;

namespace app.format.list;

/// <summary>
/// File format characteristics: extension → Kind, extension → MIME, Kind →
/// compressibility. Owned by App as <c>app.Format</c>; one per app.
///
/// Split out of <see cref="Types.@this"/> in stage 18 — Types is about
/// PLang-name ↔ CLR type identity; format characteristics are a separate
/// concern with their own home.
/// </summary>
public sealed class @this
{
    private readonly ConcurrentDictionary<string, string> _extensionToKind = new(StringComparer.OrdinalIgnoreCase)
    {
        // plang
        [".goal"] = "plang",

        // video
        [".mp4"] = "video",
        [".webm"] = "video",
        [".mkv"] = "video",
        [".mov"] = "video",
        [".avi"] = "video",
        [".flv"] = "video",

        // audio
        [".mp3"] = "audio",
        [".wav"] = "audio",
        [".flac"] = "audio",
        [".aac"] = "audio",
        [".ogg"] = "audio",
        [".m4a"] = "audio",

        // text
        [".txt"] = "text",
        [".json"] = "text",
        [".xml"] = "text",
        [".csv"] = "text",
        [".md"] = "text",
        [".yaml"] = "text",
        [".yml"] = "text",
        [".ini"] = "text",

        // image
        [".jpg"] = "image",
        [".jpeg"] = "image",
        [".png"] = "image",
        [".gif"] = "image",
        [".bmp"] = "image",
        [".tif"] = "image",
        [".tiff"] = "image",
        [".svg"] = "image",
        [".webp"] = "image",
        [".heic"] = "image",

        // archive
        [".zip"] = "archive",
        [".rar"] = "archive",
        [".7z"] = "archive",
        [".tar"] = "archive",
        [".gz"] = "archive",
        [".bz2"] = "archive",

        // spreadsheet
        [".xls"] = "spreadsheet",
        [".xlsx"] = "spreadsheet",
        [".ods"] = "spreadsheet",
        [".numbers"] = "spreadsheet",
        [".gsheet"] = "spreadsheet",

        // document
        [".doc"] = "document",
        [".docx"] = "document",
        [".odt"] = "document",
        [".pages"] = "document",
        [".gdoc"] = "document",
        [".pdf"] = "document",

        // presentation
        [".ppt"] = "presentation",
        [".pptx"] = "presentation",
        [".odp"] = "presentation",
        [".gslides"] = "presentation",

        // code
        [".cs"] = "code",
        [".js"] = "code",
        [".ts"] = "code",
        [".py"] = "code",
        [".java"] = "code",
        [".cpp"] = "code",
        [".h"] = "code",
        [".html"] = "code",
        [".css"] = "code",
        [".go"] = "code",
        [".rb"] = "code",
        [".sh"] = "code",
        [".bat"] = "code",
        [".ps1"] = "code",

        // vector
        [".ai"] = "vector",
        [".eps"] = "vector",

        // 3d-model
        [".obj"] = "3d-model",
        [".fbx"] = "3d-model",
        [".stl"] = "3d-model",
        [".gltf"] = "3d-model",
        [".glb"] = "3d-model",

        // database
        [".db"] = "database",
        [".sqlite"] = "database",
        [".mdb"] = "database",
        [".sql"] = "database",
        [".parquet"] = "database",
        [".orc"] = "database",
        [".avro"] = "database",
        [".h5"] = "database",
        [".feather"] = "database",
        [".arrow"] = "database",

        // subtitle
        [".srt"] = "subtitle",
        [".vtt"] = "subtitle",
        [".sub"] = "subtitle",

        // ebook
        [".epub"] = "ebook",
        [".mobi"] = "ebook",
        [".azw3"] = "ebook",

        // font
        [".ttf"] = "font",
        [".otf"] = "font",
        [".woff"] = "font",
        [".woff2"] = "font",

        // package
        [".msi"] = "package",
        [".deb"] = "package",
        [".rpm"] = "package",
        [".pkg"] = "package",
        [".dmg"] = "package",
        [".nupkg"] = "package",

        // disk-image
        [".iso"] = "disk-image",
        [".img"] = "disk-image",
        [".vhd"] = "disk-image",
        [".vmdk"] = "disk-image",
        [".qcow2"] = "disk-image",
        [".ova"] = "disk-image",

        // mobile-app
        [".apk"] = "mobile-app",
        [".aab"] = "mobile-app",
        [".ipa"] = "mobile-app",
        [".xapk"] = "mobile-app",

        // certificate (.key conflict resolved: "certificate" wins over "presentation")
        [".crt"] = "certificate",
        [".cer"] = "certificate",
        [".pem"] = "certificate",
        [".der"] = "certificate",
        [".p12"] = "certificate",
        [".pfx"] = "certificate",
        [".key"] = "certificate",

        // config
        [".conf"] = "config",
        [".cfg"] = "config",
        [".toml"] = "config",
        [".properties"] = "config",
        [".env"] = "config",

        // log
        [".log"] = "log",

        // machine-learning
        [".pt"] = "machine-learning",
        [".pth"] = "machine-learning",
        [".pb"] = "machine-learning",
        [".onnx"] = "machine-learning",
        [".joblib"] = "machine-learning",

        // email
        [".eml"] = "email",
        [".msg"] = "email",

        // calendar
        [".ics"] = "calendar",

        // gis-data
        [".shp"] = "gis-data",
        [".geojson"] = "gis-data",
        [".kml"] = "gis-data",
        [".gpx"] = "gis-data",

        // checksum
        [".sha256"] = "checksum",
        [".md5"] = "checksum",
        [".sfv"] = "checksum",

        // executable
        [".exe"] = "executable",
        [".dll"] = "executable",

        // binary
        [".bin"] = "binary",
    };

    private readonly ConcurrentDictionary<string, string> _extensionToMime = new(StringComparer.OrdinalIgnoreCase)
    {
        // video
        [".mp4"] = "video/mp4",
        [".webm"] = "video/webm",
        [".mkv"] = "video/x-matroska",
        [".mov"] = "video/quicktime",
        [".avi"] = "video/x-msvideo",
        [".flv"] = "video/x-flv",

        // audio
        [".mp3"] = "audio/mpeg",
        [".wav"] = "audio/wav",
        [".flac"] = "audio/flac",
        [".aac"] = "audio/aac",
        [".ogg"] = "audio/ogg",
        [".m4a"] = "audio/mp4",

        // text
        [".txt"] = "text/plain",
        [".json"] = "application/json",
        [".xml"] = "application/xml",
        [".csv"] = "text/csv",
        [".md"] = "text/markdown",
        [".yaml"] = "text/yaml",
        [".yml"] = "text/yaml",
        [".ini"] = "text/plain",
        [".llm"] = "text/plain",
        [".template"] = "text/plain",
        [".liquid"] = "text/plain",

        // plang
        // .goal is plain source text. The path-typed FilePath.ReadText still
        // exposes Goal.Parse via the .goal extension branch for callers that
        // want a typed result (discover.cs's auto-flow); the default file.read
        // action returns the raw string so existing PLang scripts that grep
        // through .goal contents keep working.
        [".goal"] = "text/plain",
        [".pr"] = "application/plang-goal",

        // image
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".bmp"] = "image/bmp",
        [".tif"] = "image/tiff",
        [".tiff"] = "image/tiff",
        [".svg"] = "image/svg+xml",
        [".webp"] = "image/webp",
        [".heic"] = "image/heic",

        // pdf
        [".pdf"] = "application/pdf",

        // archive
        [".zip"] = "application/zip",
        [".rar"] = "application/vnd.rar",
        [".7z"] = "application/x-7z-compressed",
        [".tar"] = "application/x-tar",
        [".gz"] = "application/gzip",
        [".bz2"] = "application/x-bzip2",

        // spreadsheet
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".ods"] = "application/vnd.oasis.opendocument.spreadsheet",

        // document
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".odt"] = "application/vnd.oasis.opendocument.text",

        // presentation
        [".ppt"] = "application/vnd.ms-powerpoint",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".odp"] = "application/vnd.oasis.opendocument.presentation",

        // code
        [".cs"] = "text/x-csharp",
        [".js"] = "text/javascript",
        [".ts"] = "text/typescript",
        [".py"] = "text/x-python",
        [".java"] = "text/x-java",
        [".cpp"] = "text/x-c++src",
        [".h"] = "text/x-chdr",
        [".html"] = "text/html",
        [".htm"] = "text/html",
        [".css"] = "text/css",
        [".go"] = "text/x-go",
        [".rb"] = "text/x-ruby",
        [".sh"] = "text/x-shellscript",

        // ebook
        [".epub"] = "application/epub+zip",

        // font
        [".ttf"] = "font/ttf",
        [".otf"] = "font/otf",
        [".woff"] = "font/woff",
        [".woff2"] = "font/woff2",

        // calendar
        [".ics"] = "text/calendar",

        // gis
        [".geojson"] = "application/geo+json",
        [".kml"] = "application/vnd.google-earth.kml+xml",

        // fallback
        [".bin"] = "application/octet-stream",
        [".exe"] = "application/octet-stream",
        [".dll"] = "application/octet-stream",
    };

    private static readonly HashSet<string> _notCompressible = new(StringComparer.OrdinalIgnoreCase)
    {
        "image", "video", "audio", "archive"
    };

    // Tabular content — a grid of rows/columns. Stamped as the `table` type by
    // shape (csv and xlsx are the SAME type, differing only by kind), which is
    // what lets one renderer draw a grid by dispatching on type=table alone.
    // The value is the kind the `table` reader dispatches on.
    private static readonly Dictionary<string, string> _tabularMimeToKind = new(StringComparer.OrdinalIgnoreCase)
    {
        ["text/csv"] = "csv",
        ["application/vnd.ms-excel"] = "xls",
        ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = "xlsx",
        ["application/vnd.oasis.opendocument.spreadsheet"] = "ods",
    };

    private readonly object _derivedLock = new();
    private readonly HashSet<string> _allKinds;
    private readonly ConcurrentDictionary<string, string> _mimeToKind;
    // MIME → its canonical kind (the primary file-extension form). This is what a
    // value of that MIME narrows by, so it must round-trip back to a type: the
    // subtype alone does not (text/plain's subtype is "plain", which names no type;
    // its extension "txt" does → text). First extension registered for a MIME wins.
    private readonly ConcurrentDictionary<string, string> _mimeToExtension;

    public @this()
    {
        _allKinds = new HashSet<string>(_extensionToKind.Values, StringComparer.OrdinalIgnoreCase);
        _mimeToKind = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _mimeToExtension = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in _extensionToMime)
        {
            if (_extensionToKind.TryGetValue(kvp.Key, out var kind))
                _mimeToKind.TryAdd(kvp.Value, kind);
            _mimeToExtension.TryAdd(kvp.Value, kvp.Key.TrimStart('.'));
        }
    }

    /// <summary>
    /// Family → list of extension kinds the LLM may emit for it (e.g.
    /// "image" → ["jpg","jpeg","png","gif",...]). Inverted from the
    /// extension→family map; the LLM teaching surface for "what kinds
    /// belong to what type name." Dots are stripped; original casing of
    /// the extension keys preserved.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> KindsByFamily()
    {
        var byFamily = new Dictionary<string, List<string>>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in _extensionToKind)
        {
            var family = kvp.Value;
            if (!byFamily.TryGetValue(family, out var list))
                byFamily[family] = list = new List<string>();
            var ext = kvp.Key.StartsWith('.') ? kvp.Key[1..] : kvp.Key;
            list.Add(ext);
        }
        return byFamily.ToDictionary(k => k.Key, k => (IReadOnlyList<string>)k.Value);
    }

    /// <summary>
    /// The family a kind belongs to (<c>jpg→image</c>, <c>mp3→audio</c>,
    /// <c>md→text</c>) — the type a value of this kind narrows to when no
    /// kind-specific reader names a different owner. Derived from the
    /// extension→family map. Null for an unknown kind.
    /// </summary>
    public string? TypeOf(string? kind)
    {
        if (string.IsNullOrEmpty(kind)) return null;
        return _extensionToKind.TryGetValue("." + kind, out var family) ? family : null;
    }

    /// <summary>
    /// File extension → Kind (e.g. ".jpg" → "image", ".xlsx" → "spreadsheet").
    /// Returns null for unknown or null extensions.
    /// </summary>
    public string? Kind(string extension)
    {
        if (string.IsNullOrEmpty(extension))
            return null;
        var ext = NormalizeExtension(extension);
        return _extensionToKind.TryGetValue(ext, out var kind) ? kind : null;
    }

    // --- Stage 3 accessor surface ---

    /// <summary>Index by extension. Throws on miss.</summary>
    public string this[string extension]
        => Kind(extension) ?? throw new KeyNotFoundException($"No format kind known for extension '{extension}'.");

    /// <summary>
    /// File extension → MIME content type (e.g. ".jpg" → "image/jpeg").
    /// Returns "application/octet-stream" for unknown or null extensions.
    /// </summary>
    public string Mime(string extension)
    {
        if (string.IsNullOrEmpty(extension))
            return "application/octet-stream";
        var ext = NormalizeExtension(extension);
        return _extensionToMime.TryGetValue(ext, out var mime) ? mime : "application/octet-stream";
    }

    /// <summary>
    /// The structured <c>{name, kind}</c> a read producer (file/http) stamps
    /// for content of this MIME. ONE derivation both build and runtime call so
    /// they can't drift (the bug this replaces: build said <c>type=md</c>,
    /// runtime said <c>type=text/markdown</c>).
    ///
    /// <para><c>name</c> is the family for media (image/audio/video — the value
    /// is a typed blob, not raw bytes) and otherwise the materialized CLR
    /// type's canonical PLang name (text/object/bytes/...). <c>kind</c> is the
    /// MIME subtype, canonicalised to the file-extension form
    /// (<c>markdown→md</c>). Returns the <c>type.@this.Null</c> sentinel for an
    /// unknown/octet-stream MIME so callers stamp nothing.</para>
    /// </summary>
    public global::app.type.@this TypeFromMime(string mime)
    {
        if (string.IsNullOrWhiteSpace(mime) || mime.Equals("application/octet-stream", System.StringComparison.OrdinalIgnoreCase))
            return new global::app.type.@this("binary");

        // Content off I/O is raw bytes — it IS binary; the mime's subtype is the
        // decode hint (the kind). On access the kind names the type the bytes
        // narrow to (json→item, jpg→image, csv→table) and that type's reader does
        // the parse — nothing is eagerly typed image/table/item here.
        //
        // The tabular map survives only to translate the verbose spreadsheet mimes
        // (application/vnd.ms-excel → xls) into clean kinds the subtype split can't.
        if (_tabularMimeToKind.TryGetValue(mime, out var tableKind))
            return new global::app.type.@this("binary", tableKind);

        // Kind = the MIME's canonical extension when known (text/plain → txt, so
        // it narrows back to text), else the canonicalised subtype.
        var slash = mime.IndexOf('/');
        var subtype = slash >= 0 && slash < mime.Length - 1 ? mime[(slash + 1)..] : null;
        var kind = _mimeToExtension.TryGetValue(mime, out var ext) ? ext
            : subtype != null ? CanonicaliseKind(subtype) : null;
        return new global::app.type.@this("binary", kind);
    }

    /// <summary>
    /// <see cref="TypeFromMime"/> keyed by file extension — the kind is the
    /// extension itself (the authoritative subtype for a file), the name comes
    /// from the extension's MIME. <c>.md → {text, md}</c>, <c>.json → {object,
    /// json}</c>, <c>.png → {image, png}</c>.
    /// </summary>
    public global::app.type.@this TypeFromExtension(string extension)
    {
        if (string.IsNullOrEmpty(extension)) return global::app.type.@this.Null;
        var t = TypeFromMime(Mime(extension));
        if (t.IsNull) return t;
        return new global::app.type.@this(t.Name, CanonicaliseKind(NormalizeExtension(extension).TrimStart('.')));
    }

    /// <summary>
    /// Whether content of this Kind benefits from compression. Image, video,
    /// audio, archive are already compressed — returns false.
    /// </summary>
    public bool Compressible(string kind)
    {
        if (string.IsNullOrEmpty(kind))
            return false;
        return !_notCompressible.Contains(kind);
    }

    /// <summary>
    /// PLang type value → Family ("image", "text", "spreadsheet"). Recognizes
    /// known family names and MIME types. Returns null for PLang type names
    /// (string, int, etc.) and unknown values. Renamed from <c>KindOf</c> —
    /// the formats registry called the family the "kind"; under the new
    /// vocabulary the family is the *name*, and the kind is the subtype.
    /// </summary>
    public string? FamilyOf(string typeValue)
    {
        if (string.IsNullOrEmpty(typeValue))
            return null;
        lock (_derivedLock)
        {
            if (_allKinds.TryGetValue(typeValue, out var canonical))
                return canonical;
        }
        if (typeValue.Contains('/') && _mimeToKind.TryGetValue(typeValue, out var kind))
            return kind;
        return null;
    }

    /// <summary>
    /// Canonicalises a kind token to its file-extension form. Accepts both
    /// long and short kind forms (<c>markdown</c> → <c>md</c>, <c>jpeg</c> →
    /// <c>jpg</c>). Derived from the extension→MIME registry — the inverse of
    /// the registry's MIME-subtype mapping, with the primary extension winning
    /// when two share a MIME (the <c>.jpg</c>/<c>.jpeg</c> case → <c>"jpg"</c>).
    /// Unknown free-string kinds pass through unchanged. Null/empty → null.
    /// </summary>
    public string? CanonicaliseKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind)) return null;
        var lower = kind.Trim().ToLowerInvariant();
        // Is this a MIME subtype (e.g. "markdown" for text/markdown,
        // "jpeg" for image/jpeg)? Find every extension whose MIME subtype
        // matches, pick the shortest (jpg < jpeg) — the primary extension wins
        // when two share a MIME.
        string? best = null;
        foreach (var kvp in _extensionToMime)
        {
            var mime = kvp.Value;
            var slash = mime.IndexOf('/');
            if (slash < 0) continue;
            var sub = mime[(slash + 1)..];
            if (!string.Equals(sub, lower, System.StringComparison.OrdinalIgnoreCase)) continue;
            var ext = kvp.Key;
            if (ext.StartsWith('.')) ext = ext[1..];
            if (best == null || ext.Length < best.Length) best = ext;
        }
        return best ?? lower;
    }

    /// <summary>
    /// Add a file extension mapping at runtime. Updates derived lookup
    /// structures (_allKinds, _mimeToKind) so FamilyOf() sees the new mapping.
    /// </summary>
    public void Add(string extension, string kind, string? mime = null)
    {
        var ext = NormalizeExtension(extension);
        _extensionToKind[ext] = kind;
        lock (_derivedLock) { _allKinds.Add(kind); }
        if (mime != null)
        {
            _extensionToMime[ext] = mime;
            _mimeToKind.TryAdd(mime, kind);
        }
    }

    /// <summary>
    /// Remove a file extension mapping at runtime. Cleans up derived
    /// structures: removes kind from _allKinds only if no other extension
    /// maps to it.
    /// </summary>
    public void Remove(string extension)
    {
        var ext = NormalizeExtension(extension);
        _extensionToKind.TryRemove(ext, out var removedKind);
        if (_extensionToMime.TryRemove(ext, out var removedMime))
            _mimeToKind.TryRemove(removedMime, out _);
        if (removedKind != null && !_extensionToKind.Values.Contains(removedKind, StringComparer.OrdinalIgnoreCase))
            lock (_derivedLock) { _allKinds.Remove(removedKind); }
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrEmpty(extension))
            return extension;
        return extension.StartsWith('.') ? extension : $".{extension}";
    }
}
