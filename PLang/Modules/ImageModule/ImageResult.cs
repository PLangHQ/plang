using System.ComponentModel;

namespace PLang.Modules.ImageModule;

[Description("Result from resizing or converting an image file")]
public class ImageResult
{
	[Description("Absolute path of the file that was written")]
	public string? FilePath { get; set; }

	[Description("Width in pixels of the written image")]
	public int Width { get; set; }

	[Description("Height in pixels of the written image")]
	public int Height { get; set; }

	[Description("Size of the written file in bytes")]
	public long Bytes { get; set; }

	[Description("Format of the written file: jpeg, png or webp")]
	public string? Format { get; set; }

	[Description("Width in pixels of the source image")]
	public int SourceWidth { get; set; }

	[Description("Height in pixels of the source image")]
	public int SourceHeight { get; set; }
}
