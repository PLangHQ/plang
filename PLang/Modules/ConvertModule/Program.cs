using Markdig;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Utils;
using ReverseMarkdown;
using System.ComponentModel;

namespace PLang.Modules.ConvertModule
{
	[Description("Convert object from one to another. html => md, md => html, string => keyvalue list")]
	public class Program : BaseProgram
	{

		public Program()
		{
		}

		public async Task<string?> ConvertMdToHtml(string content, bool useAdvancedExtension = true, List<string>? markdownPipelineExtensions = null)
		{
			if (string.IsNullOrWhiteSpace(content)) return null;

			var builder = new MarkdownPipelineBuilder();

			if (markdownPipelineExtensions != null && markdownPipelineExtensions.Count > 0)
			{
				foreach (var ext in markdownPipelineExtensions)
				{
					switch (ext.ToLowerInvariant())
					{
						case "abbreviations": builder.UseAbbreviations(); break;
						case "definitionlists": builder.UseDefinitionLists(); break;
						case "footnotes": builder.UseFootnotes(); break;
						case "citations": builder.UseCitations(); break;
						case "customcontainers": builder.UseCustomContainers(); break;
						case "genericattributes": builder.UseGenericAttributes(); break;
						case "gridtables": builder.UseGridTables(); break;
						case "pipetables": builder.UsePipeTables(); break;
						case "emphasisextras": builder.UseEmphasisExtras(); break;
						case "autoidentifiers": builder.UseAutoIdentifiers(); break;
						case "tasklists": builder.UseTaskLists(); break;
						case "medialinks": builder.UseMediaLinks(); break;
						case "listextras": builder.UseListExtras(); break;
						case "figures": builder.UseFigures(); break;
						case "softlinebreakashardlinebreak": builder.UseSoftlineBreakAsHardlineBreak(); break;
						default: throw new ArgumentException($"Unknown extension '{ext}'");
					}
				}
			}
			
			if (useAdvancedExtension) { 
				builder.UseAdvancedExtensions();
			}

			var pipeline = builder.Build();
			return Markdown.ToHtml(content, pipeline);
		}


		[Description("unknownTags=(Bypass|Drop|PassThrough|Raise")]
		public async Task<string?> ConvertToMd(object? content, string unknownTags = "Bypass", bool githubFlavored = true, bool removeComments = true, 
			bool smartHrefHandling = true, bool cleanupUnnecessarySpaces = true, bool suppressDivNewlines = true)
		{
			if (content == null || string.IsNullOrEmpty(content.ToString())) return null;

			if (!Enum.TryParse(unknownTags, true, out Config.UnknownTagsOption parsedOption))
			{
				parsedOption = Config.UnknownTagsOption.Bypass;
			}

			var config = new Config
			{
				UnknownTags = parsedOption,
				GithubFlavored = githubFlavored,
				RemoveComments = removeComments,
				SmartHrefHandling = smartHrefHandling,
				CleanupUnnecessarySpaces = cleanupUnnecessarySpaces,
				SuppressDivNewlines = suppressDivNewlines, 
				
			};
			var converter = new Converter(config);
			var result = converter.Convert(content.ToString());
			
			return result;
		}


		public async Task<(object?, IError?)> ConvertToKeyValueList(object variable, string newLineSeperator = "\r\n", string columnSeperator = "\t", bool trimColumns = true, List<string>? headers = null)
		{
			if (variable == null || string.IsNullOrEmpty(variable.ToString()))
			{
				return (null, new ProgramError("variable is empty", goalStep, function));
			}

			if (variable is string content)
			{
				string[] lines = content.Split(newLineSeperator);
				if (lines.Length == 0) return (null, new ProgramError($"Could not split the content in variable by newLineSeperator ({newLineSeperator})"));

				List<object> list = new();
				foreach (var line in lines)
				{
					var columns = line.Split(columnSeperator, StringSplitOptions.RemoveEmptyEntries);
					if (columns.Length == 0) continue;

					Dictionary<string, object> dict = new Dictionary<string, object>();
					for (int i = 0; i < columns.Length; i++)
					{
						dict.Add(GetHeader(headers, i), columns[i]);
					}
					list.Add(dict);
				}
				return (list, null);
			}

			ExceptionHelper.NotImplemented($"variable of type {variable.GetType()} is not supported.");
			return (null, null);

		}

		private string GetHeader(List<string>? headers, int i)
		{
			if (headers != null && headers.Count >= i) return headers[i];
			return i.ToString();
		}
	}
}
