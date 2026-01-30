using System.ComponentModel;

namespace PLang.Modules.ImageModule.QrCode;

[Description("Result from QR code generation")]
public class QrCodeResult
{
	[Description("Base64 encoded image data (for base64/png renderers)")]
	public string? Base64 { get; set; }

	[Description("Raw bytes of the image (for png renderer)")]
	public byte[]? Bytes { get; set; }

	[Description("Data URI ready for HTML img src")]
	public string? DataUri { get; set; }

	[Description("ASCII art representation (for ascii renderers)")]
	public string? Ascii { get; set; }

	[Description("SVG markup (for svg renderer)")]
	public string? Svg { get; set; }

	[Description("PostScript/EPS content (for postscript renderer)")]
	public string? PostScript { get; set; }

	[Description("Generic text output (ASCII, SVG, or PostScript depending on renderer)")]
	public string? Text { get; set; }

	[Description("File path where saved (if FilePath was provided)")]
	public string? FilePath { get; set; }
}