using System;
using System.IO;
using System.Text;
using System.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using Markdig;
using System.Collections.Generic;
using UglyToad.PdfPig.Content;
using PLang.Interfaces;
using PLang.SafeFileSystem;
using PLang.Utils;
using PLang.Building.Model;

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

	public string ConvertPdfToMarkdown(string pdfPath, string format, bool showPageNr = false, string imageAction = "none", string? password = null)
	{
		if (!fileSystem.File.Exists(pdfPath))
			throw new FileNotFoundException($"File not found: {pdfPath}");

		var markdown = new StringBuilder();
		var options = new ParsingOptions();
		if (password != null) { options.Password = password; }

		using (Stream stream =  fileSystem.File.OpenRead(pdfPath))
		using (var pdfDocument = PdfDocument.Open(stream, options))
		{
			foreach (var page in pdfDocument.GetPages())
			{
				if (showPageNr)
				{
					markdown.AppendLine($"[^0]: Page {page.Number}\n");
				}
				ExtractPageContent(page, markdown, imageAction);
				markdown.AppendLine("\n---\n");
			}
		}

		return markdown.ToString();
	}

	private void ExtractPageContent(Page page, StringBuilder markdown, string imageAction)
	{
		var text = ContentOrderTextExtractor.GetText(page);
		var lines = text.Split('\n').Select(l => l.Trim()).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

		if (lines.Count > 1)
		{
			if (DetectTable(lines))
			{
				ConvertTable(lines, markdown);
			}
			else
			{
				foreach (var line in lines)
				{
					markdown.AppendLine(line);
				}
			}
		}
		if (imageAction == "none") return;

		foreach (var image in page.GetImages())
		{
			if (imageAction == "base64")
			{
				string base64 = Convert.ToBase64String(image.RawBytes);
				string format = DetectImageFormat(image.RawBytes);
				markdown.AppendLine($"![Image](data:image/{format};base64,{base64})");
			}
			else
			{
				string imagePath = SaveImage(image, imageAction);
				markdown.AppendLine($"![Image]({imagePath})");
			}
		}
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

	private void ConvertTable(List<string> lines, StringBuilder markdown)
	{
		markdown.AppendLine();
		markdown.AppendLine(lines[0]); // Header row
		markdown.AppendLine(new string('-', lines[0].Length)); // Separator
		for (int i = 1; i < lines.Count; i++)
		{
			markdown.AppendLine(lines[i]); // Table rows
		}
		markdown.AppendLine();
	}

	private string SaveImage(IPdfImage image, string imageHandling)
	{
		var absoluteFolderPath = PathHelper.GetPath(imageHandling, fileSystem, goal);
		fileSystem.Directory.CreateDirectory(absoluteFolderPath);

		string imagePath = fileSystem.Path.Combine(absoluteFolderPath, $"img_{Guid.NewGuid()}.png");
		File.WriteAllBytes(imagePath, image.RawBytes);
		return imagePath;
	}

}
