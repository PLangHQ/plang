using Microsoft.Extensions.Configuration;
using PLang.Building.Model;
using PLang.Services.LlmService;
using PLang.Utils;

namespace PLang.Runtime.Startup
{
    internal class ModuleLoader
	{
		public static Dictionary<string, string> Modules = new Dictionary<string, string>();

		PLangLlmService openAIService;
		IConfiguration configuration;
		public ModuleLoader(PLangLlmService openAIService, IConfiguration configuration)
		{
			this.openAIService = openAIService;
			this.configuration = configuration;
		}
		public LlmQuestion GetQuestion(string content)
		{
			return new LlmQuestion("ModuleLoader",
				$@"You are deciding what modules to use in the system. 

module is the module name
type is the module type that should be used
arguments should have the scheme: {{name:string, status:object}}
keep argument name simple as possible

You MUST respond in JSON, scheme:
[{{module:string,
type:string,
arguments:object
}}]
",
content,
$@""
				);
		}

		public record Answer(string Module, string Type);


		public async Task Process()
		{
			/*PLang.Modules.FileModule.Program p = new();
			var content = await p.ReadTextFile("../../../modules.goal");
			if (content == null) { return; }
			throw new Exception("Did this run?");*/
			/*
			content = VariableHelper.LoadVariables(content);
			var question = GetQuestion(content);
			var answers = await openAIService.Query<List<Answer>>(question);
			if (answers == null || answers.Count == 0) return;

			foreach (var answer in answers) {
				Modules.Add(answer.Module, answer.Type);
			}*/

		}


	}
}
