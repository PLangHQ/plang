using LightInject;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.Modules;
using PLang.Services.SettingsService;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace PLang.Building
{
    public class SettingsBuilder
	{
		readonly IPLangFileSystem fileSystem;
		readonly ILlmService aiService;
		readonly ILogger logger;

		readonly ISettings settings;
		public SettingsBuilder(ILogger logger, IPLangFileSystem fileSystem, ILlmService aiService, ISettings settings)
		{
			

			this.fileSystem = fileSystem;
			this.aiService = aiService;
			this.logger = logger;
			this.settings = settings;
		}



		public async Task CreateSettingsPr()
		{
			//Check for BuilderHelperAttribute
			var settingsFile = Path.Combine(settings.GoalsPath, "startup", "settings.goal");

			var question = @"Settings
- use Sqlite for db
- use UTF-8 for encoding
- is dev environment / change to production when you go live
- use json as output
";
			if (fileSystem.File.Exists(settingsFile))
			{
				question = fileSystem.File.ReadAllText(settingsFile);
			}

			var llmQuestion = new LlmQuestion("SettingBuilder", @"Parse the content provided by user to determine settings in a system

- (dash) defines the module
/ (slash) defines a comment

You MUST response in JSON, scheme
[{name:string, value:string, comment:string}]

name: name of setting
value: value of the setting,  format value according to standards
comment: description of the setting

For response_type (a synonym for ContentType), format the value according to standard.
For example if value is json then it should be formatted as application/json, html is text/html

Be concise", question, @"available settings are
db_type, encoding, environment, auth_type, auth_method, variable_pre_postfix, response_type");

		
			var result = await aiService.Query<SettingConfiguration[]>(llmQuestion);

			if (!fileSystem.Directory.Exists(settings.BuildPath)) {
				fileSystem.Directory.CreateDirectory(settings.BuildPath);
			}

			fileSystem.File.WriteAllText(Path.Combine(settings.BuildPath, "Settings.pr"), JsonConvert.SerializeObject(result, Formatting.Indented));

			SettingsLoader.Load();
		}
	}
}
