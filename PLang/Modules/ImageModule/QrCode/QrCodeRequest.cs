using QRCoder;
using System.ComponentModel;

namespace PLang.Modules.ImageModule.QrCode;


[Description("Request parameters for QR code generation")]
public class QrCodeRequest
{
	[Description("The data/content to encode in the QR code")]
	public string Data { get; set; } = "";

	[Description("Type of QR code payload: text, url, email, phone, sms, wifi, vcard, geo/location")]
	public string? Type { get; set; }

	[Description("Renderer type: base64 (default), ascii, asciiSmall, svg, png, postscript/ps/eps")]
	public string? Renderer { get; set; }

	[Description("File path to save the QR code (optional)")]
	public string? FilePath { get; set; }

	[Description("Image format for base64/png renderers: png, jpg, gif (default: png)")]
	public string? Format { get; set; }

	[Description("Pixels per module/square (default: 10)")]
	public int PixelsPerModule { get; set; } = 10;

	[Description("Error correction level: L (7%), M (15%), Q (25%), H (30%) - default: Q")]
	public string? ErrorCorrection { get; set; }

	[Description("Dark/foreground color as hex (default: #000000)")]
	public string? DarkColor { get; set; }

	[Description("Light/background color as hex (default: #FFFFFF)")]
	public string? LightColor { get; set; }

	[Description("Whether to draw quiet zone border (default: true)")]
	public bool DrawQuietZones { get; set; } = true;

	[Description("Invert colors (for ascii renderers)")]
	public bool InvertColors { get; set; } = false;

	// ASCII-specific options
	[Description("Character(s) for dark modules in ASCII (default: ██)")]
	public string? AsciiDarkChar { get; set; }

	[Description("Character(s) for light modules in ASCII (default: two spaces)")]
	public string? AsciiLightChar { get; set; }

	[Description("End of line character for ASCII (default: \\n)")]
	public string? AsciiEndOfLine { get; set; }

	// SVG-specific options
	[Description("SVG sizing mode: WidthHeightAttribute, ViewBoxAttribute")]
	public SvgQRCode.SizingMode? SvgSizingMode { get; set; }

	[Description("Embedded SVG logo content")]
	public string? SvgLogo { get; set; }

	[Description("Logo size as percentage of QR code (default: 15)")]
	public int SvgLogoSizePercent { get; set; } = 15;

	// PostScript-specific options
	[Description("Generate EPS format instead of PostScript")]
	public bool EpsMode { get; set; } = false;

	// WiFi payload options
	[Description("WiFi authentication type: WPA, WEP, nopass")]
	public string? WifiAuthentication { get; set; }

	[Description("WiFi password")]
	public string? WifiPassword { get; set; }

	[Description("WiFi network is hidden")]
	public bool WifiHidden { get; set; } = false;
}
