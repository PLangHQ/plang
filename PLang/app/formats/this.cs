using System.Collections.Concurrent;

namespace app.formats;

/// <summary>
/// File format characteristics: extension → Kind, extension → MIME, Kind →
/// compressibility. Owned by App as <c>app.Formats</c>; one per app.
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

    private readonly object _derivedLock = new();
    private readonly HashSet<string> _allKinds;
    private readonly ConcurrentDictionary<string, string> _mimeToKind;

    public @this()
    {
        _allKinds = new HashSet<string>(_extensionToKind.Values, StringComparer.OrdinalIgnoreCase);
        _mimeToKind = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in _extensionToMime)
        {
            if (_extensionToKind.TryGetValue(kvp.Key, out var kind))
                _mimeToKind.TryAdd(kvp.Value, kind);
        }
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
    /// PLang type value → Kind. Recognizes known kind names and MIME types.
    /// Returns null for PLang type names (string, int, etc.) and unknown values.
    /// </summary>
    public string? KindOf(string typeValue)
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
    /// Add a file extension mapping at runtime. Updates derived lookup
    /// structures (_allKinds, _mimeToKind) so KindOf() sees the new mapping.
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
