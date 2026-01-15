using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Fizzler.Systems.HtmlAgilityPack;
using HtmlAgilityPack;
using Microsoft.Playwright;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Attributes;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Models;
using PLang.Modules.DbModule;
using PLang.Runtime;
using PLang.Utils;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace PLang.Modules.HtmlModule
{
	[Description("Parse html text, extract from css selectors")]
	public class Program : BaseProgram
	{

		public Program()
		{
		}
		[Description("Clear html from string")]
		public async Task<string> ClearHtml(string html)
		{
			return html.ClearHtml();
		}


		[Description("Extracts html content based on css selector")]
		public async Task<ObjectValue> Extract(string html, string cssSelector)
		{
			var doc = new HtmlDocument();
			doc.LoadHtml(html);
			var node = doc.DocumentNode.QuerySelector(cssSelector);
			return new HtmlObjectValue("html", node?.InnerHtml?.Trim() ?? string.Empty);
		}


		[Description("Extract an HTML table into a structured table object. Css3 selectors are availabe. FirstRowIsHeader is false by default")]
		[Example("extract table#users from %html%, first row is header, write to %users%",
		@"Parameters.Html=%html%, Parameters.CssSelector=""table#users"", Parameters.FirstRowIsHeader=true")]
		[Example("extract second .employeetable from %html%, write to %employees%",
		@"Parameters.Html=%html%, Parameters.CssSelector="".employeetable"", Parameters.FirstRowIsHeader=false")]
		[Example("extract second .employeetable from %html%, first row is header, write to %employees%",
	@"Parameters.Html=%html%, Parameters.CssSelector="".employeetable"", Parameters.Index=1, Parameters.FirstRowIsHeader=true")]
		[Example("extract last table from %html%, write to %table%",
	@"Parameters.Html=%html%, Parameters.CssSelector=""table"", Parameters.Index=-1")]
		public async Task<(object?, IError?)> ExtractTable(ExtractTableParameters parameters)
		{
			if (string.IsNullOrEmpty(parameters.Html))
			{
				return (null, new ProgramError("HTML content is required", goalStep, function));
			}

			if (string.IsNullOrEmpty(parameters.CssSelector))
			{
				return (null, new ProgramError("CSS selector is required", goalStep, function));
			}

			var parser = new HtmlParser();
			var document = parser.ParseDocument(parameters.Html);

			List<Table> tables = new();

			IEnumerable<IElement> tableNodes = document.QuerySelectorAll(parameters.CssSelector);

			if (parameters.Index != null)
			{
				IElement? element = parameters.Index == -1
					? tableNodes.LastOrDefault()
					: tableNodes.ElementAtOrDefault(parameters.Index.Value);

				if (element == null)
				{
					return (null, new ProgramError(
						$"Index {parameters.Index} out of range. Found {tableNodes.Count()} matching tables."));
				}

				tableNodes = new[] { element }.ToList();
			}

			foreach (var tableNode in tableNodes)
			{


				if (tableNode == null)
				{
					return (null, new ProgramError(
						$"Could not find table with css selector '{parameters.CssSelector}'",
						goalStep, function));
				}

				var rows = tableNode.QuerySelectorAll("tr").ToList();
				if (rows.Count == 0)
				{
					return (null, new ProgramError("Table has no rows", goalStep, function, Key: "NoRows"));
				}

				List<string> columnNames = new();
				int dataStartIndex = 0;

				if (parameters.HeaderIndex != null)
				{
					dataStartIndex += 1;
					var headerCells = rows[parameters.HeaderIndex.Value].QuerySelectorAll("th, td").ToList();
					columnNames = headerCells
						.Select((cell, idx) =>
						{
							var text = cell.TextContent?.Trim();
							return string.IsNullOrEmpty(text) ? $"Column{idx}" : text;
						})
						.ToList();
				}
				

				var table = new Table(columnNames);

				for (int i = dataStartIndex; i < rows.Count; i++)
				{
					var cells = rows[i].QuerySelectorAll("th, td").ToList();
					var row = new Row(table);

					for (int j = 0; j < cells.Count; j++)
					{
						LoadColumnName(columnNames, j);
						var cellElement = j < cells.Count ? cells[j] : null;
						var htmlValue = HtmlObjectValue.FromElement(engine, columnNames[j], cellElement, trimHtml: true);
						row[columnNames[j]] = htmlValue;
					}

					table.Add(row);
				}
				table.ColumnNames = columnNames;
				tables.Add(table);

			}
			return (tables.Count == 1) ? (tables[0], null) : (tables, null);
		}

		private void LoadColumnName(List<string> columnNames, int idx)
		{
			if (columnNames.Count < idx) return;
			columnNames.Add($"Column{idx}");
		}

		[Description("Parameters for extracting an HTML table")]
		public record ExtractTableParameters
		{
			[Description("The HTML content to parse")]
			public string Html { get; init; } = string.Empty;

			[Description("CSS selector to find the table element, e.g., 'table#users', '.data-table', 'table:first-child'")]
			public string CssSelector { get; init; } = string.Empty;

			[Description("Default is null. User might say, first row is header, then HeaderIndex=0")]
			public int? HeaderIndex { get; init; } = null;

			[Description("Zero-based index when multiple tables match. Default 0 (first match). Use -1 for last.")]
			public int? Index { get; init; } = null;


			[Description("Trims html by default")]
			public bool TrimHtml { get; init; } = true;
		}

		[Description("Extract a select element's options into a structured list. CSS3 selectors are available")]
		[Example("extract select#country from %html%, write to %countries%",
	@"Parameters.Html=%html%, Parameters.CssSelector=""select#country""")]
		[Example("extract select.user-role from %html%, write to %roles%",
	@"Parameters.Html=%html%, Parameters.CssSelector=""select.user-role""")]
		public async Task<(object?, IError?)> ExtractSelect(ExtractSelectParameters parameters)
		{
			if (string.IsNullOrEmpty(parameters.Html))
			{
				return (null, new ProgramError("HTML content is required", goalStep, function));
			}

			if (string.IsNullOrEmpty(parameters.CssSelector))
			{
				return (null, new ProgramError("CSS selector is required", goalStep, function));
			}

			var parser = new HtmlParser();
			var document = parser.ParseDocument(parameters.Html);

			var selectNodes = document.QuerySelectorAll(parameters.CssSelector).ToList();

			if (selectNodes.Count == 0)
			{
				return (null, new ProgramError(
					$"Could not find select element with css selector '{parameters.CssSelector}'",
					goalStep, function));
			}

			List<List<HtmlObjectValue>> allSelects = new();

			foreach (var selectNode in selectNodes)
			{
				var options = new List<HtmlObjectValue>();

				// Process direct option children (not in optgroup)
				foreach (var child in selectNode.Children.Where(c => c.TagName.Equals("OPTION", StringComparison.OrdinalIgnoreCase)))
				{
					var option = child as IHtmlOptionElement;
					options.Add(CreateOptionValue(option, string.Empty));
				}

				// Process optgroups
				foreach (var optgroup in selectNode.QuerySelectorAll("optgroup"))
				{
					var groupLabel = optgroup.GetAttribute("label") ?? string.Empty;

					foreach (var option in optgroup.QuerySelectorAll("option").OfType<IHtmlOptionElement>())
					{
						options.Add(CreateOptionValue(option, groupLabel));
					}
				}

				allSelects.Add(options);
			}

			return (allSelects.Count == 1) ? (allSelects[0], null) : (allSelects, null);
		}

		private HtmlObjectValue CreateOptionValue(IHtmlOptionElement? option, string groupLabel)
		{
			Dictionary<string, object> dict = new();
			dict.Add("value", option?.Value ?? string.Empty);
			dict.Add("text", option?.Text?.Trim() ?? string.Empty);
			dict.Add("html", option?.InnerHtml ?? string.Empty);
			dict.Add("group", groupLabel);
			dict.Add("selected", option?.IsSelected ?? false);
			dict.Add("disabled", option?.IsDisabled ?? false);

			var htmlValue = HtmlObjectValue.FromElement(engine, "item", option, value: dict);
			
			return htmlValue;
		}

		[Description("Parameters for extracting a select element")]
		public record ExtractSelectParameters
		{
			[Description("The HTML content to parse")]
			public string Html { get; init; } = string.Empty;

			[Description("CSS selector to find the select element, e.g., 'select#country', '.role-select', 'select[name=category]'")]
			public string CssSelector { get; init; } = string.Empty;
		}



		[Description("Extract a form element with all its inputs. CSS3 selectors are available")]
		[Example("extract form#login from %html%, write to %form%",
	@"Parameters.Html=%html%, Parameters.CssSelector=""form#login""")]
		[Example("extract form from %html%, write to %form%",
	@"Parameters.Html=%html%, Parameters.CssSelector=""form""")]
		public async Task<(object?, IError?)> ExtractForm(ExtractFormParameters parameters)
		{
			if (string.IsNullOrEmpty(parameters.Html))
			{
				return (null, new ProgramError("HTML content is required", goalStep, function));
			}

			if (string.IsNullOrEmpty(parameters.CssSelector))
			{
				return (null, new ProgramError("CSS selector is required", goalStep, function));
			}

			var parser = new HtmlParser();
			var document = parser.ParseDocument(parameters.Html);

			var formNodes = document.QuerySelectorAll(parameters.CssSelector).ToList();

			if (formNodes.Count == 0)
			{
				return (null, new ProgramError(
					$"Could not find form element with css selector '{parameters.CssSelector}'",
					goalStep, function));
			}

			List<HtmlObjectValue> allForms = new();

			foreach (var formNode in formNodes)
			{
				var formData = new Dictionary<string, object>();

				// Get all form inputs
				var inputs = formNode.QuerySelectorAll("input, textarea, select");

				foreach (var input in inputs)
				{
					var name = input.GetAttribute("name");
					if (string.IsNullOrEmpty(name)) continue;

					object? value = input switch
					{
						IHtmlInputElement inp => inp.Type?.ToLowerInvariant() switch
						{
							"checkbox" => inp.IsChecked,
							"radio" => inp.IsChecked ? inp.Value : (formData.ContainsKey(name) ? formData[name] : null),
							"number" => inp.Value,
							"file" => inp.Value, // just the filename
							_ => inp.Value ?? string.Empty
						},
						IHtmlTextAreaElement textarea => textarea.Value ?? string.Empty,
						IHtmlSelectElement select => select.IsMultiple
							? select.Options.Where(o => o.IsSelected).Select(o => o.Value).ToList()
							: select.Value ?? string.Empty,
						_ => input.GetAttribute("value") ?? string.Empty
					};

					// For radio, only set if checked or not yet set
					if (input is IHtmlInputElement radioInput && radioInput.Type?.ToLowerInvariant() == "radio")
					{
						if (radioInput.IsChecked)
						{
							formData[name] = value!;
						}
						else if (!formData.ContainsKey(name))
						{
							formData[name] = string.Empty;
						}
					}
					else
					{
						formData[name] = value!;
					}
				}

				var formValue = HtmlObjectValue.FromElement(engine, "form", formNode as IElement, value: formData);
				allForms.Add(formValue);
			}

			return (allForms.Count == 1) ? (allForms[0], null) : (allForms, null);
		}

		[Description("Parameters for extracting a form element")]
		public record ExtractFormParameters
		{
			[Description("The HTML content to parse")]
			public string Html { get; init; } = string.Empty;

			[Description("CSS selector to find the form element, e.g., 'form#login', 'form.signup', 'form'")]
			public string CssSelector { get; init; } = string.Empty;
		}

	}
}
