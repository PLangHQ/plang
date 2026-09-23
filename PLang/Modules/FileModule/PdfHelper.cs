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

	public List<PdfPage> ConvertPdfToMarkdown(string pdfPath, string format, string? imagePath = null, string? password = null)
	{
		if (!fileSystem.File.Exists(pdfPath))
			throw new FileNotFoundException($"File not found: {pdfPath}");

		var options = new ParsingOptions();
		if (password != null) { options.Password = password; }

		List<PdfPage> pages = new();
		
		using (Stream stream =  fileSystem.File.OpenRead(pdfPath))
		using (var pdfDocument = PdfDocument.Open(stream, options))
		{
			foreach (var page in pdfDocument.GetPages())
			{	
				var lines = (format == "layout" ? ExtractPageLayout(page) : ExtractPageContent(page))
					.Select(l => l.Replace('\0', '�'));
				IEnumerable<string> images = new List<string>();
				if (!string.IsNullOrEmpty(imagePath))
				{
					images = GetImages(page, imagePath);
				}

				var pdfPage = new PdfPage(lines, images, page.Number, (int)page.Size);
				pages.Add(pdfPage);
			}
		}

		return pages;
	}

	public PdfProperties GetProperties(string pdfPath, string? password = null)
	{
		var options = new ParsingOptions();
		if (password != null) { options.Password = password; }

		using (Stream stream = fileSystem.File.OpenRead(pdfPath))
		using (var pdfDocument = PdfDocument.Open(stream, options))
		{
			var info = pdfDocument.Information;

			return new PdfProperties
			{
				PageCount = pdfDocument.NumberOfPages,
				Version = pdfDocument.Version.ToString(),
				Title = info.Title,
				Author = info.Author,
				Subject = info.Subject,
				Keywords = info.Keywords,
				Creator = info.Creator,
				Producer = info.Producer,
				CreationDate = info.CreationDate,
				ModificationDate = info.ModifiedDate,
				IsEncrypted = pdfDocument.IsEncrypted,
				Pages = pdfDocument.GetPages().Select(p => new PageInfo
				{
					PageNumber = p.Number,
					Width = p.Width,
					Height = p.Height,
					Rotation = p.Rotation.Value
				}).ToList()
			};
		}
	}

	public class PdfProperties
	{
		public int PageCount { get; set; }
		public string? Version { get; set; }
		public string? Title { get; set; }
		public string? Author { get; set; }
		public string? Subject { get; set; }
		public string? Keywords { get; set; }
		public string? Creator { get; set; }
		public string? Producer { get; set; }
		public string? CreationDate { get; set; }
		public string? ModificationDate { get; set; }
		public bool IsEncrypted { get; set; }
		public List<PageInfo>? Pages { get; set; }
	}

	public class PageInfo
	{
		public int PageNumber { get; set; }
		public double Width { get; set; }
		public double Height { get; set; }
		public int Rotation { get; set; }
	}

	private IEnumerable<string>? ExtractPageContent(Page page)
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

	// Keeps the horizontal position of every word, the way pdftotext -layout does, so tables whose
	// columns are only implied by x position (a lab report with one column per draw date) survive
	// extraction. The reading order extractor loses that: a row with five values under six date
	// columns comes out as five numbers with no way to tell which column is empty.
	private IEnumerable<string> ExtractPageLayout(Page page)
	{
		var words = page.GetWords().Where(w => !string.IsNullOrWhiteSpace(w.Text)).ToList();
		if (words.Count == 0) return [""];

		double unit = words.Average(w => w.BoundingBox.Width / Math.Max(1, w.Text.Length));
		if (unit <= 0) unit = 4;
		double lineTolerance = words.Average(w => w.BoundingBox.Height) * 0.5;

		var rows = new List<(double y, List<Word> words)>();
		foreach (var word in words.OrderByDescending(w => w.BoundingBox.Bottom))
		{
			var row = rows.LastOrDefault();
			if (rows.Count > 0 && Math.Abs(row.y - word.BoundingBox.Bottom) <= lineTolerance)
			{
				row.words.Add(word);
			}
			else
			{
				rows.Add((word.BoundingBox.Bottom, new List<Word> { word }));
			}
		}

		var lines = new List<string>();
		foreach (var row in rows)
		{
			var sb = new StringBuilder();
			foreach (var word in row.words.OrderBy(w => w.BoundingBox.Left))
			{
				int column = (int)Math.Round(word.BoundingBox.Left / unit);
				if (sb.Length < column) sb.Append(' ', column - sb.Length);
				else if (sb.Length > 0) sb.Append(' ');
				sb.Append(word.Text);
			}
			lines.Add(sb.ToString().TrimEnd());
		}
		return lines;
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
