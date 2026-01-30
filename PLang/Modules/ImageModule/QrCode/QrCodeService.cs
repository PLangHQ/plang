using PLang.Models;
using PLang.Runtime;
using QRCoder;
using SixLabors.ImageSharp;
using System.IO.Abstractions;

namespace PLang.Modules.ImageModule.QrCode;

public class QrCodeService
{
	private readonly IFileSystem _fileSystem;

	public QrCodeService(IFileSystem fileSystem)
	{
		_fileSystem = fileSystem;
	}

	public async Task<(QrCodeResult Result, Properties Props)> Generate(QrCodeRequest request)
	{
		var eccLevel = ParseEccLevel(request.ErrorCorrection);
		var payloadData = CreatePayload(request);

		QRCodeData qrCodeData;
		using (var qrGenerator = new QRCodeGenerator())
		{
			qrCodeData = qrGenerator.CreateQrCode(payloadData, eccLevel);
		}

		var result = new QrCodeResult();
		var props = new Properties();

		props.Add(new ObjectValue("type", request.Type ?? "text"));
		props.Add(new ObjectValue("errorCorrection", eccLevel.ToString()));
		props.Add(new ObjectValue("renderer", request.Renderer ?? "base64"));

		var renderer = request.Renderer?.ToLowerInvariant() ?? "base64";

		switch (renderer)
		{
			case "ascii":
				GenerateAscii(qrCodeData, request, result, props);
				break;
			case "svg":
				GenerateSvg(qrCodeData, request, result, props);
				break;
			case "postscript":
			case "ps":
			case "eps":
				GeneratePostScript(qrCodeData, request, result, props);
				break;
			case "png":
			case "pngbyte":
				GeneratePngByte(qrCodeData, request, result, props);
				break;
			case "base64":
			default:
				GenerateBase64(qrCodeData, request, result, props);
				break;
		}

		if (!string.IsNullOrEmpty(request.FilePath))
		{
			await SaveToFile(request.FilePath, result, props);
		}

		return (result, props);
	}

	private void GenerateAscii(QRCodeData qrCodeData, QrCodeRequest request, QrCodeResult result, Properties props)
	{
		var qrCode = new AsciiQRCode(qrCodeData);
		var darkChar = request.AsciiDarkChar ?? "██";
		var lightChar = request.AsciiLightChar ?? "  ";
		var endOfLine = request.AsciiEndOfLine ?? "\n";

		result.Ascii = qrCode.GetGraphic(1, darkChar, lightChar, endOfLine: endOfLine);
		result.Text = result.Ascii;

		props.Add(new ObjectValue("darkChar", darkChar));
		props.Add(new ObjectValue("lightChar", lightChar));
	}

	private void GenerateSvg(QRCodeData qrCodeData, QrCodeRequest request, QrCodeResult result, Properties props)
	{
		var qrCode = new SvgQRCode(qrCodeData);
		var darkColor = request.DarkColor ?? "#000000";
		var lightColor = request.LightColor ?? "#ffffff";

		result.Svg = qrCode.GetGraphic(
			request.PixelsPerModule,
			darkColor,
			lightColor,
			request.DrawQuietZones,
			request.SvgSizingMode ?? SvgQRCode.SizingMode.WidthHeightAttribute,
			request.SvgLogo != null ? new SvgQRCode.SvgLogo(request.SvgLogo, request.SvgLogoSizePercent) : null
		);
		result.Text = result.Svg;
		result.DataUri = $"data:image/svg+xml;base64,{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(result.Svg))}";

		props.Add(new ObjectValue("pixelsPerModule", request.PixelsPerModule));
		props.Add(new ObjectValue("darkColor", darkColor));
		props.Add(new ObjectValue("lightColor", lightColor));
	}

	private void GeneratePostScript(QRCodeData qrCodeData, QrCodeRequest request, QrCodeResult result, Properties props)
	{
		var qrCode = new PostscriptQRCode(qrCodeData);
		var darkColor = request.DarkColor ?? "#000000";
		var lightColor = request.LightColor ?? "#ffffff";

		result.PostScript = qrCode.GetGraphic(
			request.PixelsPerModule,
			darkColor,
			lightColor,
			request.DrawQuietZones,
			request.EpsMode
		);
		result.Text = result.PostScript;

		props.Add(new ObjectValue("epsMode", request.EpsMode));
	}

	private void GeneratePngByte(QRCodeData qrCodeData, QrCodeRequest request, QrCodeResult result, Properties props)
	{
		var qrCode = new PngByteQRCode(qrCodeData);
		var darkColorBytes = ParseColorToBytes(request.DarkColor, new byte[] { 0, 0, 0 });
		var lightColorBytes = ParseColorToBytes(request.LightColor, new byte[] { 255, 255, 255 });

		result.Bytes = qrCode.GetGraphic(
			request.PixelsPerModule,
			darkColorBytes,
			lightColorBytes,
			request.DrawQuietZones
		);
		result.Base64 = Convert.ToBase64String(result.Bytes);
		result.DataUri = $"data:image/png;base64,{result.Base64}";

		props.Add(new ObjectValue("pixelsPerModule", request.PixelsPerModule));
		props.Add(new ObjectValue("format", "png"));
	}

	private void GenerateBase64(QRCodeData qrCodeData, QrCodeRequest request, QrCodeResult result, Properties props)
	{
		var darkColor = ParseColor(request.DarkColor, Color.Black);
		var lightColor = ParseColor(request.LightColor, Color.White);
		var imgType = GetImageType(request.FilePath, request.Format);

		var qrCode = new Base64QRCode(qrCodeData);
		result.Base64 = qrCode.GetGraphic(
			request.PixelsPerModule,
			darkColor,
			lightColor,
			request.DrawQuietZones,
			imgType
		);
		result.DataUri = $"data:image/{imgType.ToString().ToLower()};base64,{result.Base64}";
		result.Bytes = Convert.FromBase64String(result.Base64);

		props.Add(new ObjectValue("pixelsPerModule", request.PixelsPerModule));
		props.Add(new ObjectValue("darkColor", request.DarkColor ?? "#000000"));
		props.Add(new ObjectValue("lightColor", request.LightColor ?? "#FFFFFF"));
		props.Add(new ObjectValue("format", imgType.ToString()));
	}

	private async Task SaveToFile(string filePath, QrCodeResult result, Properties props)
	{
		var fullPath = _fileSystem.Path.GetFullPath(filePath);

		var directory = _fileSystem.Path.GetDirectoryName(fullPath);
		if (!string.IsNullOrEmpty(directory) && !_fileSystem.Directory.Exists(directory))
		{
			_fileSystem.Directory.CreateDirectory(directory);
		}

		if (result.Bytes?.Length > 0)
		{
			await _fileSystem.File.WriteAllBytesAsync(fullPath, result.Bytes);
		}
		else if (!string.IsNullOrEmpty(result.Base64))
		{
			await _fileSystem.File.WriteAllBytesAsync(fullPath, Convert.FromBase64String(result.Base64));
		}
		else if (!string.IsNullOrEmpty(result.Text))
		{
			await _fileSystem.File.WriteAllTextAsync(fullPath, result.Text);
		}

		result.FilePath = fullPath;
		props.Add(new ObjectValue("filePath", fullPath));
	}

	private string CreatePayload(QrCodeRequest request)
	{
		return request.Type?.ToLowerInvariant() switch
		{
			"url" => request.Data,
			"email" => $"mailto:{request.Data}",
			"phone" or "tel" => $"tel:{request.Data}",
			"sms" => $"sms:{request.Data}",
			"wifi" => CreateWifiPayload(request),
			"vcard" => request.Data,
			"geo" or "location" => request.Data.Contains(",") ? $"geo:{request.Data}" : request.Data,
			_ => request.Data
		};
	}

	private string CreateWifiPayload(QrCodeRequest request)
	{
		if (request.Data.StartsWith("WIFI:", StringComparison.OrdinalIgnoreCase))
			return request.Data;

		var auth = request.WifiAuthentication ?? "WPA";
		var hidden = request.WifiHidden ? "true" : "false";
		return $"WIFI:T:{auth};S:{request.Data};P:{request.WifiPassword ?? ""};H:{hidden};;";
	}

	private QRCodeGenerator.ECCLevel ParseEccLevel(string? errorCorrection)
	{
		return errorCorrection?.ToUpperInvariant() switch
		{
			"L" => QRCodeGenerator.ECCLevel.L,
			"M" => QRCodeGenerator.ECCLevel.M,
			"Q" => QRCodeGenerator.ECCLevel.Q,
			"H" => QRCodeGenerator.ECCLevel.H,
			_ => QRCodeGenerator.ECCLevel.Q
		};
	}

	private Color ParseColor(string? colorString, Color defaultColor)
	{
		if (string.IsNullOrEmpty(colorString))
			return defaultColor;

		return Color.TryParse(colorString, out var color) ? color : defaultColor;
	}

	private byte[] ParseColorToBytes(string? colorString, byte[] defaultColor)
	{
		if (string.IsNullOrEmpty(colorString))
			return defaultColor;

		var hex = colorString.TrimStart('#');
		if (hex.Length == 3)
			hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";

		if (hex.Length == 6)
		{
			return new byte[]
			{
				Convert.ToByte(hex.Substring(0, 2), 16),
				Convert.ToByte(hex.Substring(2, 2), 16),
				Convert.ToByte(hex.Substring(4, 2), 16)
			};
		}

		return defaultColor;
	}

	private Base64QRCode.ImageType GetImageType(string? filePath, string? requestedFormat)
	{
		var format = requestedFormat?.ToLowerInvariant()
			?? Path.GetExtension(filePath)?.TrimStart('.').ToLowerInvariant()
			?? "png";

		return format switch
		{
			"jpg" or "jpeg" => Base64QRCode.ImageType.Jpeg,
			"gif" => Base64QRCode.ImageType.Gif,
			_ => Base64QRCode.ImageType.Png
		};
	}
}