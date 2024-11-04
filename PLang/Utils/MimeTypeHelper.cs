using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Utils
{
	public class MimeTypeHelper
	{
		public static string GetMimeType(string fileName)
		{
			var mimeType = GetWebMimeType(fileName);
			if (mimeType == null) return "application/octet-stream";
			return mimeType;
		}
		public static string? GetWebMimeType(string fileName)
		{
			string extension = Path.GetExtension(fileName).ToLowerInvariant();

			string? mimeType = extension switch
			{
				".mp3" => "audio/mpeg",
				".wav" => "audio/wav",
				".ogg" => "audio/ogg",
				".m4a" => "audio/mp4",
				".aac" => "audio/aac",
				".midi" => "audio/midi",
				".mid" => "audio/midi",
				".flac" => "audio/flac",
				".weba" => "audio/webm",
				".mp4" => "video/mp4",
				".avi" => "video/x-msvideo",
				".mpeg" => "video/mpeg",
				".ogv" => "video/ogg",
				".webm" => "video/webm",
				".3gp" => "video/3gpp",
				".3g2" => "video/3gpp2",
				".mkv" => "video/x-matroska",
				".jpeg" => "image/jpeg",
				".jpg" => "image/jpeg",
				".png" => "image/png",
				".gif" => "image/gif",
				".bmp" => "image/bmp",
				".svg" => "image/svg+xml",
				".webp" => "image/webp",
				".ico" => "image/vnd.microsoft.icon",
				".tif, .tiff" => "image/tiff",
				".txt" => "text/plain",
				".html, .htm" => "text/html",
				".css" => "text/css",
				".csv" => "text/csv",
				".json" => "application/json",
				".pdf" => "application/pdf",
				".doc" => "application/msword",
				".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
				".xls" => "application/vnd.ms-excel",
				".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
				".ppt" => "application/vnd.ms-powerpoint",
				".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
				".xml" => "application/xml",
				".zip" => "application/zip",
				".tar" => "application/x-tar",
				".rar" => "application/vnd.rar",
				".7z" => "application/x-7z-compressed",
				".js" => "application/javascript",
				_ => null
			};
			return mimeType;
		}
	}
}
