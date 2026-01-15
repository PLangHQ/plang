using Markdig;
using Markdig.Helpers;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.SafeFileSystem;
using PLang.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using static PLang.Modules.FileModule.Program;
using static System.Net.Mime.MediaTypeNames;

namespace PLang.Modules.FileModule;

public class PdfToMarkdownConverter
{
	private readonly IPLangFileSystem fileSystem;
	private readonly Goal goal;

	public PdfToMarkdownConverter(IPLangFileSystem fileSystem, Goal goal)
	{
		this.fileSystem = fileSystem;
		this.goal = goal;
	}

	public enum ImageHandling { Skip, Base64, SaveToPath }

	public List<PdfPage> ConvertPdfToMarkdown(string pdfPath, string format, bool showPageNr = false, string imageAction = "none", string? password = null)
	{
		if (!fileSystem.File.Exists(pdfPath))
			throw new FileNotFoundException($"File not found: {pdfPath}");

		var markdown = new StringBuilder();
		var options = new ParsingOptions();
		if (password != null) { options.Password = password; }

		List<PdfPage> pages = new();
		
		using (Stream stream =  fileSystem.File.OpenRead(pdfPath))
		using (var pdfDocument = PdfDocument.Open(stream, options))
		{
			foreach (var page in pdfDocument.GetPages())
			{	
				var lines = ExtractPageContent(page, markdown, imageAction);
				var images = GetImages(page, imageAction);

				var pdfPage = new PdfPage(lines, images, page.Number, (int)page.Size);
				pages.Add(pdfPage);
			}
		}

		return pages;
	}

	private IEnumerable<string>? ExtractPageContent(Page page, StringBuilder markdown, string imageAction)
	{

		var text = ContentOrderTextExtractor.GetText(page);
		var lines = text.Split('\n').Select(l => l.Trim()).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

		if (lines.Count > 1)
		{
			if (DetectTable(lines))
			{
				return ConvertTable(lines);
			}
			else
			{
				if (int.TryParse(lines[lines.Count - 1].Trim(), out int pageNr) && pageNr == page.Number)
				{
					return lines[..^1];
				}
				return lines;
				
			}
		}
		return [""];

	}

	private IEnumerable<string> GetImages(Page page, string imageAction) {
		if (string.IsNullOrEmpty(imageAction)) return [];

		List<string> images = new();
		foreach (var image in page.GetImages())
		{
			if (imageAction == "base64")
			{
				string base64 = Convert.ToBase64String(image.RawBytes);
				string format = DetectImageFormat(image.RawBytes);
				images.Add($"data:image/{format};base64,{base64})");
			}
			else
			{
				string imagePath = SaveImage(image, imageAction);
				images.Add(imagePath);
			}
		}
		return images;
	}

	private static string DetectImageFormat(ReadOnlySpan<byte> imageData)
	{
		if (imageData.Length > 3 && imageData[0] == 0xFF && imageData[1] == 0xD8 && imageData[2] == 0xFF)
			return "jpeg"; // JPEG detected

		if (imageData.Length > 7 && imageData[0] == 0x89 && imageData[1] == 0x50 && imageData[2] == 0x4E && imageData[3] == 0x47)
			return "png"; // PNG detected

		return "png"; // Default to PNG if unknown
	}

	private bool DetectTable(List<string> lines)
	{
		return lines.Count > 2 && lines.All(l => l.Contains("|"));
	}

	private List<string> ConvertTable(List<string> lines)
	{
		List<string> returnLines = new();
		lines.Add(lines[0]); // Header row
		returnLines.Add(new string('-', lines[0].Length)); // Separator
		for (int i = 1; i < lines.Count; i++)
		{
			returnLines.Add(lines[i]); // Table rows
		}
		return returnLines;
	}

	private string SaveImage(IPdfImage image, string imageHandling)
	{
		var absoluteFolderPath = PathHelper.GetPath(imageHandling, fileSystem, goal);
		fileSystem.Directory.CreateDirectory(absoluteFolderPath);

		string imagePath = fileSystem.Path.Join(absoluteFolderPath, $"img_{Guid.NewGuid()}.png");
		fileSystem.File.WriteAllBytes(imagePath, image.RawBytes);
		return imagePath;
	}

}
