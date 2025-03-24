using PLang.Errors;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Services.SettingsService;
using System.ComponentModel;
using ReverseMarkdown;

namespace PLang.Modules.ConvertModule
{
	[Description("Convert object from one to another")]
	public class Program : BaseProgram
	{

		public Program()
		{
		}

		[Description("unknownTags=(Bypass|Drop|PassThrough|Raise")]
		public async Task<string?> ConvertToMd(object content, string unknownTags = "Bypass", bool githubFlavored = true, bool removeComments = true, 
			bool smartHrefHandling = true, bool cleanupUnnecessarySpaces = true, bool suppressDivNewlines = true)
		{
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
			return converter.Convert(content.ToString());
		}

	}
}
