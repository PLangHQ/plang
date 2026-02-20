using System.Reflection;
using System.Text.Json.Serialization;

namespace PLang.Runtime2.Engine.Types;

/// <summary>
/// Owns all type knowledge in PLang Runtime2.
/// Consolidates PLang names ↔ CLR types, file extension → Kind, extension → MIME,
/// and Kind → compressibility into a single live instance on Engine.
/// </summary>
public sealed class @this
{
    private readonly Dictionary<string, System.Type> _nameToClr = new(StringComparer.OrdinalIgnoreCase)
    {
        // Primitives
        ["string"] = typeof(string),
        ["text"] = typeof(string),
        ["int"] = typeof(int),
        ["integer"] = typeof(int),
        ["long"] = typeof(long),
        ["float"] = typeof(float),
        ["double"] = typeof(double),
        ["decimal"] = typeof(decimal),
        ["bool"] = typeof(bool),
        ["boolean"] = typeof(bool),
        ["datetime"] = typeof(DateTime),
        ["date"] = typeof(DateTime),
        ["time"] = typeof(TimeSpan),
        ["timespan"] = typeof(TimeSpan),
        ["guid"] = typeof(Guid),
        ["byte"] = typeof(byte),
        ["bytes"] = typeof(byte[]),

        // Collections
        ["list"] = typeof(List<object>),
        ["array"] = typeof(object[]),
        ["dictionary"] = typeof(Dictionary<string, object>),
        ["dict"] = typeof(Dictionary<string, object>),
        ["map"] = typeof(Dictionary<string, object>),
        ["object"] = typeof(object),
        ["dynamic"] = typeof(object),
        ["json"] = typeof(System.Text.Json.Nodes.JsonNode),
        ["json[]"] = typeof(System.Text.Json.Nodes.JsonArray),
        ["actor"] = typeof(Context.Actor),
        ["goal.call"] = typeof(Goals.Goal.GoalCall),
        ["tstring"] = typeof(Memory.TString),
        ["translatable"] = typeof(Memory.TString),

        // Nullable types
        ["int?"] = typeof(int?),
        ["long?"] = typeof(long?),
        ["double?"] = typeof(double?),
        ["bool?"] = typeof(bool?),
        ["datetime?"] = typeof(DateTime?),
        ["guid?"] = typeof(Guid?),
    };

    private readonly Dictionary<System.Type, string> _clrToName = new()
    {
        [typeof(string)] = "string",
        [typeof(int)] = "int",
        [typeof(long)] = "long",
        [typeof(float)] = "float",
        [typeof(double)] = "double",
        [typeof(decimal)] = "decimal",
        [typeof(bool)] = "bool",
        [typeof(DateTime)] = "datetime",
        [typeof(TimeSpan)] = "timespan",
        [typeof(Guid)] = "guid",
        [typeof(byte)] = "byte",
        [typeof(byte[])] = "bytes",
        [typeof(object)] = "object",
        [typeof(Goals.Goal.GoalCall)] = "goal.call",
        [typeof(Memory.TString)] = "tstring",
    };

    private readonly Dictionary<string, string> _extensionToKind = new(StringComparer.OrdinalIgnoreCase)
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

    private readonly Dictionary<string, string> _extensionToMime = new(StringComparer.OrdinalIgnoreCase)
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

    private readonly HashSet<string> _allKinds;
    private readonly Dictionary<string, string> _mimeToKind;

    public @this()
    {
        _allKinds = new HashSet<string>(_extensionToKind.Values, StringComparer.OrdinalIgnoreCase);
        _mimeToKind = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in _extensionToMime)
        {
            if (_extensionToKind.TryGetValue(kvp.Key, out var kind))
                _mimeToKind.TryAdd(kvp.Value, kind);
        }
    }

    /// <summary>
    /// PLang type name → CLR type.
    /// Handles generics (list&lt;string&gt;), dictionaries (dict&lt;K,V&gt;), nullable (int?), and MIME types.
    /// </summary>
    public System.Type? Clr(string plangName)
    {
        if (string.IsNullOrWhiteSpace(plangName))
            return null;

        // Handle generic list syntax: list<string>
        if (plangName.StartsWith("list<", StringComparison.OrdinalIgnoreCase) && plangName.EndsWith(">"))
        {
            var innerTypeName = plangName[5..^1];
            var innerType = Clr(innerTypeName) ?? typeof(object);
            return typeof(List<>).MakeGenericType(innerType);
        }

        // Handle generic dictionary syntax: dict<string,int>
        if ((plangName.StartsWith("dict<", StringComparison.OrdinalIgnoreCase) ||
             plangName.StartsWith("dictionary<", StringComparison.OrdinalIgnoreCase)) && plangName.EndsWith(">"))
        {
            var prefix = plangName.StartsWith("dict<", StringComparison.OrdinalIgnoreCase) ? 5 : 11;
            var inner = plangName[prefix..^1];
            var parts = inner.Split(',');
            if (parts.Length == 2)
            {
                var keyType = Clr(parts[0].Trim()) ?? typeof(string);
                var valueType = Clr(parts[1].Trim()) ?? typeof(object);
                return typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
            }
        }

        if (_nameToClr.TryGetValue(plangName, out var type))
            return type;

        // MIME type resolution
        if (plangName.Contains('/'))
        {
            if (plangName.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
                return typeof(string);
            if (plangName.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
                plangName.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ||
                plangName.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
                return typeof(byte[]);
            if (plangName.Equals("application/json", StringComparison.OrdinalIgnoreCase))
                return typeof(object);
            if (plangName.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
                return typeof(byte[]);
        }

        return null;
    }

    /// <summary>
    /// CLR type → PLang type name.
    /// Handles nullable, generics, arrays, and ValidValues convention types.
    /// </summary>
    public string Name(System.Type clrType)
    {
        if (clrType == null)
            return "object";

        // Handle nullable types
        var underlying = Nullable.GetUnderlyingType(clrType);
        if (underlying != null)
            return Name(underlying) + "?";

        // Handle generic types
        if (clrType.IsGenericType)
        {
            var generic = clrType.GetGenericTypeDefinition();
            if (generic == typeof(List<>) || generic == typeof(IList<>))
                return $"list<{Name(clrType.GetGenericArguments()[0])}>";
            if (generic == typeof(Dictionary<,>) || generic == typeof(IDictionary<,>))
            {
                var args = clrType.GetGenericArguments();
                return $"dict<{Name(args[0])},{Name(args[1])}>";
            }
        }

        // Handle arrays
        if (clrType.IsArray)
        {
            var elementType = clrType.GetElementType()!;
            if (elementType == typeof(byte))
                return "bytes";
            return $"list<{Name(elementType)}>";
        }

        if (_clrToName.TryGetValue(clrType, out var name))
            return name;

        // Strip arity suffix from generic types (e.g. "HashSet`1" → "hashset")
        var typeName = clrType.Name;
        var backtickIndex = typeName.IndexOf('`');
        if (backtickIndex >= 0)
            typeName = typeName[..backtickIndex];

        // Check for ValidValues static property (convention for constrained types)
        var validValuesProp = clrType.GetProperty("ValidValues",
            BindingFlags.Public | BindingFlags.Static);
        if (validValuesProp != null && validValuesProp.PropertyType == typeof(string[]))
            return typeName.ToLowerInvariant();

        return typeName.ToLowerInvariant();
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
    /// Whether content of this Kind benefits from compression.
    /// Image, video, audio, archive are already compressed — returns false.
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
        if (_allKinds.TryGetValue(typeValue, out var canonical))
            return canonical;
        if (typeValue.Contains('/') && _mimeToKind.TryGetValue(typeValue, out var kind))
            return kind;
        return null;
    }

    /// <summary>
    /// Add a file extension mapping at runtime.
    /// Updates derived lookup structures (_allKinds, _mimeToKind) so KindOf() sees the new mapping.
    /// </summary>
    public void Add(string extension, string kind, string? mime = null)
    {
        var ext = NormalizeExtension(extension);
        _extensionToKind[ext] = kind;
        _allKinds.Add(kind);
        if (mime != null)
        {
            _extensionToMime[ext] = mime;
            _mimeToKind.TryAdd(mime, kind);
        }
    }

    /// <summary>
    /// Remove a file extension mapping at runtime.
    /// Cleans up derived structures: removes kind from _allKinds only if no other extension maps to it.
    /// </summary>
    public void Remove(string extension)
    {
        var ext = NormalizeExtension(extension);
        _extensionToKind.Remove(ext, out var removedKind);
        if (_extensionToMime.Remove(ext, out var removedMime))
            _mimeToKind.Remove(removedMime);
        // Only remove from _allKinds if no other extension maps to this kind
        if (removedKind != null && !_extensionToKind.ContainsValue(removedKind))
            _allKinds.Remove(removedKind);
    }

    /// <summary>
    /// Returns canonical builder type names (excludes aliases like "text"→"string").
    /// Keeps shortest name per CLR type, skips nullable variants.
    /// </summary>
    public List<string> BuilderNames()
    {
        var seen = new HashSet<System.Type>();
        var names = new List<string>();
        foreach (var kvp in _nameToClr)
        {
            if (kvp.Key.EndsWith("?")) continue;
            if (seen.Contains(kvp.Value)) continue;
            seen.Add(kvp.Value);

            var validValues = ValidValues(kvp.Value);
            if (validValues != null)
                names.Add($"{kvp.Key}({string.Join("|", validValues)})");
            else
                names.Add(kvp.Key);
        }
        return names;
    }

    /// <summary>
    /// Returns schemas for complex types (goal.call, etc.) based on [LlmBuilder] attributes.
    /// </summary>
    public Dictionary<string, string> ComplexSchemas()
    {
        var schemas = new Dictionary<string, string>();
        foreach (var kvp in _nameToClr)
        {
            var name = kvp.Key;
            var type = kvp.Value;
            if (name.EndsWith("?") || Utility.TypeMapping.IsPrimitive(type) || type == typeof(object)) continue;
            if (type.IsArray || type.IsGenericType) continue;
            if (ValidValues(type) != null) continue;

            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.Name != "EqualityContract")
                .Where(p => Attribute.IsDefined(p, typeof(LlmBuilderAttribute)))
                .Where(p => !Attribute.IsDefined(p, typeof(JsonIgnoreAttribute)))
                .Select(p => $"{char.ToLower(p.Name[0]) + p.Name[1..]}: {Name(p.PropertyType)}");

            if (props.Any())
                schemas[name] = $"{{ {string.Join(", ", props)} }}";
        }
        return schemas;
    }

    /// <summary>
    /// Gets the valid values for a constrained type (e.g. Actor → ["user","service","system"]).
    /// Returns null if the type has no ValidValues convention property.
    /// </summary>
    public static string[]? ValidValues(System.Type type)
    {
        var prop = type.GetProperty("ValidValues",
            BindingFlags.Public | BindingFlags.Static);
        if (prop != null && prop.PropertyType == typeof(string[]))
            return (string[])prop.GetValue(null)!;
        return null;
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrEmpty(extension))
            return extension;
        return extension.StartsWith('.') ? extension : $".{extension}";
    }
}
