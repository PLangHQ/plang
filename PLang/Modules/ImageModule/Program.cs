using NBitcoin.Protocol;
using PLang.Attributes;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Models;
using PLang.Modules;
using PLang.Modules.ImageModule.QrCode;
using PLang.Runtime;
using QRCoder;
using SixLabors.ImageSharp;
using System.ComponentModel;

namespace PLang.Modules.ImageModule
{
	[Description("Image generation and manipulation including QR codes, barcodes, and image processing")]
	public class Program : BaseProgram
	{
		public Program() : base() { }

		[Description("Generates a QR code from data with multiple output formats")]
		[Example("generate qr code from %data% save to %filePath%",
			@"QrCodeRequest.Data=%data%, QrCodeRequest.FilePath=%filePath%")]
		[Example("make qr using %url%, type: url, write to %base64%",
			@"QrCodeRequest.Data=%url%, QrCodeRequest.Type=""url""")]
		[Example("create ascii qr code from %text%, write to %ascii%",
			@"QrCodeRequest.Data=%text%, QrCodeRequest.Renderer=""ascii""")]
		[Example("generate svg qr from %data%, write to %svg%",
			@"QrCodeRequest.Data=%data%, QrCodeRequest.Renderer=""svg""")]
		[Example("create qr code from %text%, renderer: art, background style: circles, write to %result%",
			@"QrCodeRequest.Data=%text%, QrCodeRequest.Renderer=""art"", QrCodeRequest.BackgroundStyle=""circles""")]
		[Example("generate qr for %payload%, error correction: H, dark color: #ff0000, write to %qr%",
			@"QrCodeRequest.Data=%payload%, QrCodeRequest.ErrorCorrection=""H"", QrCodeRequest.DarkColor=""#ff0000""")]
		public async Task<(QrCodeResult?, IError?, Properties?)> GenerateQrCode(QrCodeRequest request)
		{
			if (string.IsNullOrEmpty(request.Data))
			{
				return (null, new ProgramError("Data is required for QR code generation", goalStep, function), null);
			}

			try
			{

				request.FilePath = GetPath(request.FilePath);

				var eccLevel = ParseEccLevel(request.ErrorCorrection);
				var payloadData = CreatePayload(request);

				QRCodeData qrCodeData;
				using (var qrGenerator = new QRCodeGenerator())
				{
					qrCodeData = qrGenerator.CreateQrCode(payloadData, eccLevel);
				}

				var result = new QrCodeResult();
				var props = new Properties();

				// Add common metadata
				props.Add(new ObjectValue("type", request.Type ?? "text"));
				props.Add(new ObjectValue("errorCorrection", eccLevel.ToString()));
				props.Add(new ObjectValue("renderer", request.Renderer ?? "base64"));

				// Generate based on renderer type
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
						await GeneratePngByte(qrCodeData, request, result, props);
						break;

					case "base64":
					default:
						await GenerateBase64(qrCodeData, request, result, props);
						break;
				}

				// Save to file if path provided and we have content
				if (!string.IsNullOrEmpty(request.FilePath))
				{
					// this should be using FileModule
					await SaveToFile(request.FilePath, result, props);
				}

				return (result, null, props);
			}
			catch (Exception ex)
			{
				return (null, new ProgramError($"Failed to generate QR code: {ex.Message}", goalStep, function, Exception: ex), null);
			}
		}

		private void GenerateAscii(QRCodeData qrCodeData, QrCodeRequest request, QrCodeResult result, Properties props)
		{
			var qrCode = new AsciiQRCode(qrCodeData);

			var darkChar = request.AsciiDarkChar ?? "██";
			var lightChar = request.AsciiLightChar ?? "  ";
			string endOfLine = request.AsciiEndOfLine ?? "\n";

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

		private async Task GeneratePngByte(QRCodeData qrCodeData, QrCodeRequest request, QrCodeResult result, Properties props)
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

		private async Task GenerateBase64(QRCodeData qrCodeData, QrCodeRequest request, QrCodeResult result, Properties props)
		{
			var darkColor = ParseColor(request.DarkColor, SixLabors.ImageSharp.Color.Black);
			var lightColor = ParseColor(request.LightColor, SixLabors.ImageSharp.Color.White);
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
			var fullPath = fileSystem.Path.GetFullPath(filePath);

			var directory = fileSystem.Path.GetDirectoryName(fullPath);
			if (!string.IsNullOrEmpty(directory) && !fileSystem.Directory.Exists(directory))
			{
				fileSystem.Directory.CreateDirectory(directory);
			}

			var ext = Path.GetExtension(filePath).ToLowerInvariant();

			if (result.Bytes != null || result.Bytes?.Length > 0)
			{
				await fileSystem.File.WriteAllBytesAsync(fullPath, result.Bytes);
			}
			else if (!string.IsNullOrEmpty(result.Base64))
			{
				var bytes = Convert.FromBase64String(result.Base64);
				await fileSystem.File.WriteAllBytesAsync(fullPath, bytes);
			}
			else if (!string.IsNullOrEmpty(result.Text))
			{
				await fileSystem.File.WriteAllTextAsync(fullPath, result.Text);
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

		private SixLabors.ImageSharp.Color ParseColor(string? colorString, SixLabors.ImageSharp.Color defaultColor)
		{
			if (string.IsNullOrEmpty(colorString))
				return defaultColor;

			if (SixLabors.ImageSharp.Color.TryParse(colorString, out var color))
				return color;

			return defaultColor;
		}

		private byte[] ParseColorToBytes(string? colorString, byte[] defaultColor)
		{
			if (string.IsNullOrEmpty(colorString))
				return defaultColor;

			// Parse hex color like #RRGGBB or #RGB
			var hex = colorString.TrimStart('#');
			if (hex.Length == 3)
			{
				hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
			}

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

	
}