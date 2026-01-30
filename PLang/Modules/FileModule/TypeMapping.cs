using PLang.Errors;

namespace PLang.Modules.FileModule;

public interface ITypeMapping
{
	(string?, IError?) GetType(string extension);
	string? GetContentType(string extension);
	void AddMapping(string extension, string type, string? contentType = null);
}

public class TypeMapping : ITypeMapping
{
	private readonly Dictionary<string, string> typeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
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
		[".key"] = "presentation",
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

		// certificate
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
		[".bin"] = "binary"
	};

	private readonly Dictionary<string, string> contentTypeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
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
		[".dll"] = "application/octet-stream"
	};

	public (string?, IError?) GetType(string extension)
	{
		var ext = extension.StartsWith('.') ? extension : $".{extension}";
		if (typeMap.TryGetValue(ext, out var type)) {
			return (type, null);
		}

		return (null, new Error($"Type does not exist for {extension}"));
	}

	public string? GetContentType(string extension)
	{
		var ext = extension.StartsWith('.') ? extension : $".{extension}";
		return contentTypeMap.TryGetValue(ext, out var contentType) ? contentType : "application/octet-stream";
	}

	public void AddMapping(string extension, string type, string? contentType = null)
	{
		var ext = extension.StartsWith('.') ? extension : $".{extension}";
		typeMap[ext] = type;

		if (contentType != null)
		{
			contentTypeMap[ext] = contentType;
		}
	}
}