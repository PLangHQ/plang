using PLang.Attributes;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Models;
using PLang.Modules.ImageModule.QrCode;
using PLang.Runtime;
using QRCoder;
using SkiaSharp;
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
						GeneratePngByte(qrCodeData, request, result, props);
						break;

					case "base64":
					default:
						GenerateBase64(qrCodeData, request, result, props);
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

			var (encodedFormat, formatName, formatError) = GetEncoder(format, target);
			if (formatError != null) return (null, formatError);

			try
			{
				byte[] bytes = await fileSystem.File.ReadAllBytesAsync(source);
				var (decoded, readError) = Decode(bytes, sourcePath);
				if (readError != null) return (null, readError);

				var image = decoded!;
				var result = new ImageResult { SourceWidth = image.Width, SourceHeight = image.Height, Format = formatName };
				var background = ParseBackground(backgroundColor, formatName);

				var (newWidth, newHeight, resizeMode) = CalculateSize(image.Width, image.Height, width, height, mode);
				if (newWidth != image.Width || newHeight != image.Height)
				{
					image = Replace(image, ResizeBitmap(image, newWidth, newHeight));
				}

				if (resizeMode == "fill" && width > 0 && height > 0 && (image.Width != width || image.Height != height))
				{
					int cropX = Math.Max(0, (image.Width - width) / 2);
					int cropY = Math.Max(0, (image.Height - height) / 2);
					int cropW = Math.Min(width, image.Width);
					int cropH = Math.Min(height, image.Height);
					image = Replace(image, Draw(cropW, cropH, SKColors.Transparent, canvas => canvas.DrawBitmap(image, -cropX, -cropY)));
				}

				bool padToBox = (resizeMode == "pad" || center) && width > 0 && height > 0;
				if (padToBox && (image.Width != width || image.Height != height))
				{
					var top = image;
					image = Replace(image, Draw(width, height, background, canvas => canvas.DrawBitmap(top, (width - top.Width) / 2, (height - top.Height) / 2)));
				}

				if (sharpen) image = Replace(image, Sharpen(image));
				if (formatName == "jpeg")
				{
					var flat = image;
					image = Replace(image, Draw(flat.Width, flat.Height, background, canvas => canvas.DrawBitmap(flat, 0, 0)));
				}

				using (image)
				{
					return await Write(image, encodedFormat, quality, target, result);
				}
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
			using var codec = SKCodec.Create(new MemoryStream(await fileSystem.File.ReadAllBytesAsync(source)));
			if (codec == null)
			{
				return (null, new ProgramError($"The file is not an image that can be read: {sourcePath}", goalStep, function, StatusCode: 400));
			}
			// The size the image is shown in: a photo stored on its side (EXIF orientation 5-8) is turned before it is written.
			var (shownWidth, shownHeight) = IsSideways(codec.EncodedOrigin) ? (codec.Info.Height, codec.Info.Width) : (codec.Info.Width, codec.Info.Height);
			return await Resize(sourcePath, shownWidth, shownHeight, targetPath, "exact", quality, format, backgroundColor);
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
				using var codec = SKCodec.Create(new MemoryStream(bytes));
				if (codec == null)
				{
					return (null, new ProgramError($"The file is not an image that can be read: {sourcePath}", goalStep, function, StatusCode: 400));
				}
				var info = codec.Info;
				var name = codec.EncodedFormat.ToString().ToLowerInvariant();
				return (new ImageResult { FilePath = source, Width = info.Width, Height = info.Height, SourceWidth = info.Width, SourceHeight = info.Height, Bytes = bytes.Length, Format = name }, null);
			}
			catch (Exception ex)
			{
				return (null, new ProgramError($"Failed to read {sourcePath}: {ex.Message}", goalStep, function, Exception: ex));
			}
		}

		// Reads the image and turns it the way its EXIF orientation says, so it is upright.
		private (SKBitmap?, IError?) Decode(byte[] bytes, string sourcePath)
		{
			using var codec = SKCodec.Create(new MemoryStream(bytes));
			if (codec == null)
			{
				return (null, new ProgramError($"The file is not an image that can be read: {sourcePath}", goalStep, function, StatusCode: 400));
			}
			var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
			var bitmap = new SKBitmap(info);
			var decodeResult = codec.GetPixels(info, bitmap.GetPixels());
			if (decodeResult != SKCodecResult.Success && decodeResult != SKCodecResult.IncompleteInput)
			{
				bitmap.Dispose();
				return (null, new ProgramError($"The image is damaged and cannot be read: {sourcePath}. {decodeResult}", goalStep, function, StatusCode: 400));
			}
			return (Orient(bitmap, codec.EncodedOrigin), null);
		}

		private static bool IsSideways(SKEncodedOrigin origin) =>
			origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

		private static SKBitmap Orient(SKBitmap bitmap, SKEncodedOrigin origin)
		{
			if (origin == SKEncodedOrigin.TopLeft) return bitmap;

			float w = bitmap.Width, h = bitmap.Height;
			// x' = scaleX * x + skewX * y + transX, y' = skewY * x + scaleY * y + transY
			var matrix = origin switch
			{
				SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
				SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
				SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
				SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
				SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
				SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
				SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
				_ => SKMatrix.Identity
			};
			int newWidth = IsSideways(origin) ? bitmap.Height : bitmap.Width;
			int newHeight = IsSideways(origin) ? bitmap.Width : bitmap.Height;
			return Replace(bitmap, Draw(newWidth, newHeight, SKColors.Transparent, canvas =>
			{
				canvas.SetMatrix(matrix);
				canvas.DrawBitmap(bitmap, 0, 0);
			}));
		}

		// A large reduction in one cubic pass skips source pixels and aliases; halving first (each halving averages 2x2)
		// keeps every pixel in the result, and the last step to the exact size is bicubic (Catmull-Rom).
		private static SKBitmap ResizeBitmap(SKBitmap source, int width, int height)
		{
			var current = source;
			while (current.Width / 2 >= width && current.Height / 2 >= height)
			{
				var half = current.Resize(new SKImageInfo(current.Width / 2, current.Height / 2, SKColorType.Rgba8888, SKAlphaType.Premul), new SKSamplingOptions(SKFilterMode.Linear));
				if (current != source) current.Dispose();
				current = half;
			}
			var resized = current.Resize(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul), new SKSamplingOptions(SKCubicResampler.CatmullRom));
			if (current != source) current.Dispose();
			return resized;
		}

		private static SKBitmap Sharpen(SKBitmap source)
		{
			float[] kernel = [0, -1, 0, -1, 5, -1, 0, -1, 0];
			using var filter = SKImageFilter.CreateMatrixConvolution(new SKSizeI(3, 3), kernel, 1f, 0f, new SKPointI(1, 1), SKShaderTileMode.Clamp, false);
			using var paint = new SKPaint { ImageFilter = filter };
			return Draw(source.Width, source.Height, SKColors.Transparent, canvas => canvas.DrawBitmap(source, 0, 0, paint));
		}

		private static SKBitmap Draw(int width, int height, SKColor background, Action<SKCanvas> draw)
		{
			var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
			using var canvas = new SKCanvas(bitmap);
			canvas.Clear(background);
			draw(canvas);
			canvas.Flush();
			return bitmap;
		}

		private static SKBitmap Replace(SKBitmap old, SKBitmap replacement)
		{
			if (!ReferenceEquals(old, replacement)) old.Dispose();
			return replacement;
		}

		private async Task<(ImageResult?, IError?)> Write(SKBitmap image, SKEncodedImageFormat format, int quality, string target, ImageResult result)
		{
			var directory = fileSystem.Path.GetDirectoryName(target);
			if (!string.IsNullOrEmpty(directory) && !fileSystem.Directory.Exists(directory))
			{
				fileSystem.Directory.CreateDirectory(directory);
			}
			using var data = image.Encode(format, quality);
			if (data == null)
			{
				return (null, new ProgramError($"Could not write the image as {format.ToString().ToLowerInvariant()}: {target}", goalStep, function));
			}
			var bytes = data.ToArray();
			await fileSystem.File.WriteAllBytesAsync(target, bytes);
			result.FilePath = target;
			result.Width = image.Width;
			result.Height = image.Height;
			result.Bytes = bytes.Length;
			return (result, null);
		}

		private (SKEncodedImageFormat, string?, IError?) GetEncoder(string? format, string target)
		{
			var name = (format ?? "").Trim().TrimStart('.').ToLowerInvariant();
			if (name == "") name = Path.GetExtension(target).TrimStart('.').ToLowerInvariant();
			switch (name)
			{
				case "jpg":
				case "jpeg":
					return (SKEncodedImageFormat.Jpeg, "jpeg", null);
				case "png":
					return (SKEncodedImageFormat.Png, "png", null);
				case "webp":
					return (SKEncodedImageFormat.Webp, "webp", null);
				default:
					return (default, null, new ProgramError($"Unsupported image format '{name}'. Use jpeg, png or webp, or give the target file one of those extensions", goalStep, function, StatusCode: 400));
			}
		}

		private static SKColor ParseBackground(string? backgroundColor, string? formatName)
		{
			var color = ParseColor(backgroundColor);
			if (color != null) return color.Value;
			return formatName == "jpeg" ? SKColors.White : SKColors.Transparent;
		}

		// A color as a CSS name (white, red, transparent) or hex: #rgb, #rgba, #rrggbb or #rrggbbaa (alpha last).
		private static SKColor? ParseColor(string? value)
		{
			if (string.IsNullOrWhiteSpace(value)) return null;
			var text = value.Trim();
			var hex = text.TrimStart('#');
			if (hex.Length is 3 or 4 or 6 or 8 && hex.All(Uri.IsHexDigit))
			{
				if (hex.Length <= 4) hex = string.Concat(hex.Select(c => $"{c}{c}"));
				byte Part(int i) => Convert.ToByte(hex.Substring(i * 2, 2), 16);
				return new SKColor(Part(0), Part(1), Part(2), hex.Length == 8 ? Part(3) : (byte)255);
			}
			var named = System.Drawing.Color.FromName(text.Replace(" ", ""));
			if (named.IsKnownColor) return new SKColor(named.R, named.G, named.B, named.A);
			return null;
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

		private void GeneratePngByte(QRCodeData qrCodeData, QrCodeRequest request, QrCodeResult result, Properties props)
		{
			result.Bytes = RenderPng(qrCodeData, request);
			result.Base64 = Convert.ToBase64String(result.Bytes);
			result.DataUri = $"data:image/png;base64,{result.Base64}";

			props.Add(new ObjectValue("pixelsPerModule", request.PixelsPerModule));
			props.Add(new ObjectValue("format", "png"));
		}

		// PNG from QRCoder itself; a jpeg is that PNG encoded again. Gif is not written by SkiaSharp, so a gif request gets a PNG.
		private void GenerateBase64(QRCodeData qrCodeData, QrCodeRequest request, QrCodeResult result, Properties props)
		{
			var png = RenderPng(qrCodeData, request);
			var imgType = GetImageType(request.FilePath, request.Format);
			if (imgType == "jpeg")
			{
				using var bitmap = SKBitmap.Decode(png);
				using var flat = Draw(bitmap.Width, bitmap.Height, SKColors.White, canvas => canvas.DrawBitmap(bitmap, 0, 0));
				using var data = flat.Encode(SKEncodedImageFormat.Jpeg, 100);
				result.Bytes = data.ToArray();
			}
			else
			{
				imgType = "png";
				result.Bytes = png;
			}
			result.Base64 = Convert.ToBase64String(result.Bytes);
			result.DataUri = $"data:image/{imgType};base64,{result.Base64}";

			props.Add(new ObjectValue("pixelsPerModule", request.PixelsPerModule));
			props.Add(new ObjectValue("darkColor", request.DarkColor ?? "#000000"));
			props.Add(new ObjectValue("lightColor", request.LightColor ?? "#FFFFFF"));
			props.Add(new ObjectValue("format", imgType == "jpeg" ? "Jpeg" : "Png"));
		}

		private static byte[] RenderPng(QRCodeData qrCodeData, QrCodeRequest request)
		{
			var dark = ParseColor(request.DarkColor) ?? SKColors.Black;
			var light = ParseColor(request.LightColor) ?? SKColors.White;
			var qrCode = new PngByteQRCode(qrCodeData);
			return qrCode.GetGraphic(
				request.PixelsPerModule,
				System.Drawing.Color.FromArgb(dark.Alpha, dark.Red, dark.Green, dark.Blue),
				System.Drawing.Color.FromArgb(light.Alpha, light.Red, light.Green, light.Blue),
				request.DrawQuietZones);
		}

		private async Task SaveToFile(string filePath, QrCodeResult result, Properties props)
		{
			var fullPath = fileSystem.Path.GetFullPath(filePath);

			var directory = fileSystem.Path.GetDirectoryName(fullPath);
			if (!string.IsNullOrEmpty(directory) && !fileSystem.Directory.Exists(directory))
			{
				fileSystem.Directory.CreateDirectory(directory);
			}

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

		private static string GetImageType(string? filePath, string? requestedFormat)
		{
			var format = requestedFormat?.ToLowerInvariant()
				?? Path.GetExtension(filePath)?.TrimStart('.').ToLowerInvariant()
				?? "png";

			return format is "jpg" or "jpeg" ? "jpeg" : "png";
		}
	}
}
