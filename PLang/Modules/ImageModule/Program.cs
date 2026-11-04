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
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.ComponentModel;

namespace PLang.Modules.ImageModule
{
	[Description("Image generation and manipulation including QR codes, barcodes, and image processing")]
	[Effect(Effects.WritesFiles)]
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

				// A step that only wants the data uri gives no path, and resolving an empty one
				// returned the app root, which the renderers then tried to write to: every such
				// step failed with "Access to the path '/the/app' is denied".
				if (!string.IsNullOrEmpty(request.FilePath)) request.FilePath = GetPath(request.FilePath);

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

		[Description("Resizes an image file to fit a width and height and writes the result to a new file. The mode says how the image meets the box: fit keeps the aspect ratio and fits inside the box (may enlarge), shrink-only is fit that never enlarges a smaller image, fill covers the whole box and crops the overflow, exact and stretch force the exact size and distort, pad keeps the aspect ratio and pads the rest of the box with the background color. The format is taken from the target file extension (jpg, png, webp) unless format is given")]
		[Example("resize photo.jpg by 685x420, shrink only, quality 100, write to out.jpg",
			@"sourcePath=""photo.jpg"", width=685, height=420, targetPath=""out.jpg"", mode=""shrink-only"", quality=100")]
		[Example("resize %file% to 80x80 and save as /thumbs/%name%.jpg",
			@"sourcePath=%file%, width=80, height=80, targetPath=""/thumbs/%name%.jpg""")]
		[Example("make a thumbnail of banner.png 300 wide, write to small.webp, quality 80",
			@"sourcePath=""banner.png"", width=300, height=0, targetPath=""small.webp"", quality=80")]
		[Example("resize cover.jpg to 500x500, fill, sharpen, write to square.jpg",
			@"sourcePath=""cover.jpg"", width=500, height=500, targetPath=""square.jpg"", mode=""fill"", sharpen=true")]
		[Example("resize logo.png by 400x400, pad with white background, write to padded.jpg",
			@"sourcePath=""logo.png"", width=400, height=400, targetPath=""padded.jpg"", mode=""pad"", backgroundColor=""white""")]
		[Example("breyta stærð á mynd.jpg í 230x400, aldrei stækka, vista sem lítil.jpg",
			@"sourcePath=""mynd.jpg"", width=230, height=400, targetPath=""lítil.jpg"", mode=""shrink-only""")]
		public async Task<(ImageResult?, IError?)> Resize(
			[Description("Path of the image file to resize")] string sourcePath,
			[Description("Width of the box in pixels. 0 means it follows from the height and the aspect ratio. At least one of width and height must be above 0")] int width,
			[Description("Height of the box in pixels. 0 means it follows from the width and the aspect ratio")] int height,
			[Description("Path of the file to write. Its extension decides the format unless format is given. It may be the same as the source path")] string targetPath,
			[Description("fit, shrink-only, fill, exact, stretch or pad. Use shrink-only when the step says never enlarge, only shrink or do not enlarge. Use fill when it says cover, crop or fill. Use fit when nothing is said")] string mode = "fit",
			[Description("Quality 1 to 100 for jpeg and webp. Ignored for png")] int quality = 90,
			[Description("jpeg, png or webp. Empty means the target file extension")] string? format = null,
			[Description("Background color for pad mode and for flattening transparency when the target is jpeg, for example white, black or #ff0000. Empty means white for jpeg and transparent for png and webp")] string? backgroundColor = null,
			[Description("true when the step says sharpen")] bool sharpen = false,
			[Description("true when the step says center on a canvas of exactly width x height, so the result has the box size and the resized image sits in the middle on the background color")] bool center = false)
		{
			if (width < 0 || height < 0 || (width == 0 && height == 0))
			{
				return (null, new ProgramError($"Width and height must be positive and at least one above 0, got {width}x{height}", goalStep, function));
			}
			if (quality < 1 || quality > 100)
			{
				return (null, new ProgramError($"Quality must be between 1 and 100, got {quality}", goalStep, function));
			}

			var source = GetPath(sourcePath);
			var target = GetPath(targetPath);
			if (!fileSystem.File.Exists(source))
			{
				return (null, new ProgramError($"Image file not found: {sourcePath}", goalStep, function, StatusCode: 404));
			}

			var (encoder, formatName, formatError) = GetEncoder(format, target, quality);
			if (formatError != null) return (null, formatError);

			try
			{
				byte[] bytes = await fileSystem.File.ReadAllBytesAsync(source);
				using var image = SixLabors.ImageSharp.Image.Load(bytes);
				image.Mutate(x => x.AutoOrient());

				var result = new ImageResult { SourceWidth = image.Width, SourceHeight = image.Height, Format = formatName };
				var background = ParseBackground(backgroundColor, formatName);

				var (newWidth, newHeight, resizeMode) = CalculateSize(image.Width, image.Height, width, height, mode);
				if (newWidth != image.Width || newHeight != image.Height)
				{
					image.Mutate(x => x.Resize(new ResizeOptions
					{
						Size = new SixLabors.ImageSharp.Size(newWidth, newHeight),
						Mode = ResizeMode.Stretch,
						Sampler = KnownResamplers.Bicubic
					}));
				}

				if (resizeMode == "fill" && width > 0 && height > 0 && (image.Width != width || image.Height != height))
				{
					int cropX = Math.Max(0, (image.Width - width) / 2);
					int cropY = Math.Max(0, (image.Height - height) / 2);
					int cropW = Math.Min(width, image.Width);
					int cropH = Math.Min(height, image.Height);
					image.Mutate(x => x.Crop(new SixLabors.ImageSharp.Rectangle(cropX, cropY, cropW, cropH)));
				}

				bool padToBox = (resizeMode == "pad" || center) && width > 0 && height > 0;
				if (padToBox && (image.Width != width || image.Height != height))
				{
					var canvas = new SixLabors.ImageSharp.Image<Rgba32>(width, height, background.ToPixel<Rgba32>());
					var point = new SixLabors.ImageSharp.Point((width - image.Width) / 2, (height - image.Height) / 2);
					using var top = image.CloneAs<Rgba32>();
					canvas.Mutate(x => x.DrawImage(top, point, 1f));
					image.Dispose();
					return await Save(canvas, encoder, target, formatName!, sharpen, background, result);
				}

				return await Save(image, encoder, target, formatName!, sharpen, background, result);
			}
			catch (SixLabors.ImageSharp.UnknownImageFormatException)
			{
				return (null, new ProgramError($"The file is not an image that can be read: {sourcePath}", goalStep, function, StatusCode: 400));
			}
			catch (SixLabors.ImageSharp.InvalidImageContentException ex)
			{
				return (null, new ProgramError($"The image is damaged and cannot be read: {sourcePath}. {ex.Message}", goalStep, function, Exception: ex, StatusCode: 400));
			}
			catch (Exception ex)
			{
				return (null, new ProgramError($"Failed to resize {sourcePath}: {ex.Message}", goalStep, function, Exception: ex));
			}
		}

		[Description("Converts an image file to another format (jpeg, png or webp) without changing its size and writes it to a new file")]
		[Example("convert photo.png to jpg, quality 85, write to photo.jpg",
			@"sourcePath=""photo.png"", targetPath=""photo.jpg"", quality=85")]
		[Example("convert %file% to webp and save as out.webp",
			@"sourcePath=%file%, targetPath=""out.webp""")]
		public async Task<(ImageResult?, IError?)> ConvertImage(
			[Description("Path of the image file to convert")] string sourcePath,
			[Description("Path of the file to write. Its extension decides the format unless format is given")] string targetPath,
			[Description("Quality 1 to 100 for jpeg and webp")] int quality = 90,
			[Description("jpeg, png or webp. Empty means the target file extension")] string? format = null,
			[Description("Background color used to flatten transparency when the target is jpeg. Empty means white")] string? backgroundColor = null)
		{
			var source = GetPath(sourcePath);
			if (!fileSystem.File.Exists(source))
			{
				return (null, new ProgramError($"Image file not found: {sourcePath}", goalStep, function, StatusCode: 404));
			}
			var probe = SixLabors.ImageSharp.Image.Identify(await fileSystem.File.ReadAllBytesAsync(source));
			return await Resize(sourcePath, probe.Width, probe.Height, targetPath, "exact", quality, format, backgroundColor);
		}

		[Description("Reads the width, height and format of an image file without changing it")]
		[Example("get image info of %file%, write to %info%", @"sourcePath=%file%")]
		[Effect(Effects.None)]
		public async Task<(ImageResult?, IError?)> GetImageInfo([Description("Path of the image file")] string sourcePath)
		{
			var source = GetPath(sourcePath);
			if (!fileSystem.File.Exists(source))
			{
				return (null, new ProgramError($"Image file not found: {sourcePath}", goalStep, function, StatusCode: 404));
			}
			try
			{
				var bytes = await fileSystem.File.ReadAllBytesAsync(source);
				var info = SixLabors.ImageSharp.Image.Identify(bytes);
				var name = SixLabors.ImageSharp.Image.DetectFormat(bytes)?.Name?.ToLowerInvariant();
				if (name == "jpg") name = "jpeg";
				return (new ImageResult { FilePath = source, Width = info.Width, Height = info.Height, SourceWidth = info.Width, SourceHeight = info.Height, Bytes = bytes.Length, Format = name }, null);
			}
			catch (SixLabors.ImageSharp.UnknownImageFormatException)
			{
				return (null, new ProgramError($"The file is not an image that can be read: {sourcePath}", goalStep, function, StatusCode: 400));
			}
			catch (Exception ex)
			{
				return (null, new ProgramError($"Failed to read {sourcePath}: {ex.Message}", goalStep, function, Exception: ex));
			}
		}

		private async Task<(ImageResult?, IError?)> Save(SixLabors.ImageSharp.Image image, SixLabors.ImageSharp.Formats.IImageEncoder encoder, string target, string formatName, bool sharpen, SixLabors.ImageSharp.Color background, ImageResult result)
		{
			if (sharpen) image.Mutate(x => x.GaussianSharpen());

			if (formatName == "jpeg")
			{
				using var flat = image.CloneAs<Rgba32>();
				var white = new SixLabors.ImageSharp.Image<Rgba32>(flat.Width, flat.Height, background.ToPixel<Rgba32>());
				white.Mutate(x => x.DrawImage(flat, 1f));
				image.Dispose();
				return await Write(white, encoder, target, result);
			}
			return await Write(image, encoder, target, result);
		}

		private async Task<(ImageResult?, IError?)> Write(SixLabors.ImageSharp.Image image, SixLabors.ImageSharp.Formats.IImageEncoder encoder, string target, ImageResult result)
		{
			using (image)
			{
				var directory = fileSystem.Path.GetDirectoryName(target);
				if (!string.IsNullOrEmpty(directory) && !fileSystem.Directory.Exists(directory))
				{
					fileSystem.Directory.CreateDirectory(directory);
				}
				using var ms = new MemoryStream();
				await image.SaveAsync(ms, encoder);
				await fileSystem.File.WriteAllBytesAsync(target, ms.ToArray());
				result.FilePath = target;
				result.Width = image.Width;
				result.Height = image.Height;
				result.Bytes = ms.Length;
			}
			return (result, null);
		}

		private (SixLabors.ImageSharp.Formats.IImageEncoder, string?, IError?) GetEncoder(string? format, string target, int quality)
		{
			var name = (format ?? "").Trim().TrimStart('.').ToLowerInvariant();
			if (name == "") name = Path.GetExtension(target).TrimStart('.').ToLowerInvariant();
			switch (name)
			{
				case "jpg":
				case "jpeg":
					return (new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = quality }, "jpeg", null);
				case "png":
					return (new SixLabors.ImageSharp.Formats.Png.PngEncoder(), "png", null);
				case "webp":
					return (new SixLabors.ImageSharp.Formats.Webp.WebpEncoder { Quality = quality }, "webp", null);
				default:
					return (null!, null, new ProgramError($"Unsupported image format '{name}'. Use jpeg, png or webp, or give the target file one of those extensions", goalStep, function, StatusCode: 400));
			}
		}

		private SixLabors.ImageSharp.Color ParseBackground(string? backgroundColor, string? formatName)
		{
			if (!string.IsNullOrWhiteSpace(backgroundColor) && SixLabors.ImageSharp.Color.TryParse(backgroundColor.Trim(), out var color)) return color;
			return formatName == "jpeg" ? SixLabors.ImageSharp.Color.White : SixLabors.ImageSharp.Color.Transparent;
		}

		private static (int, int, string) CalculateSize(int sourceWidth, int sourceHeight, int width, int height, string? mode)
		{
			var m = (mode ?? "fit").Trim().ToLowerInvariant().Replace('_', '-').Replace(' ', '-');
			if (m == "shrink" || m == "shrinkonly") m = "shrink-only";
			if (m == "crop" || m == "cover") m = "fill";
			if (m == "stretch") m = "exact";

			if (m == "exact")
			{
				int w = width > 0 ? width : (int)Math.Round(sourceWidth * (height / (double)sourceHeight), MidpointRounding.AwayFromZero);
				int h = height > 0 ? height : (int)Math.Round(sourceHeight * (width / (double)sourceWidth), MidpointRounding.AwayFromZero);
				return (Math.Max(w, 1), Math.Max(h, 1), "exact");
			}

			var scales = new List<double>();
			if (width > 0) scales.Add(width / (double)sourceWidth);
			if (height > 0) scales.Add(height / (double)sourceHeight);
			double scale = m == "fill" ? scales.Max() : scales.Min();
			if (m == "shrink-only") scale = Math.Min(scale, 1d);

			int newWidth = Math.Max((int)Math.Round(sourceWidth * scale, MidpointRounding.AwayFromZero), 1);
			int newHeight = Math.Max((int)Math.Round(sourceHeight * scale, MidpointRounding.AwayFromZero), 1);
			return (newWidth, newHeight, m == "fill" ? "fill" : m == "pad" ? "pad" : "fit");
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