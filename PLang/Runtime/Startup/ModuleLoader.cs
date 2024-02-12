using PLang.Models;

namespace PLang.Runtime.Startup
{
	internal class ModuleLoader
	{
		public static Dictionary<string, string> Modules = new Dictionary<string, string>();

		public ModuleLoader()
		{
		}
		public LlmRequest GetQuestion(string content)
		{
			var promptMessage = new List<LlmMessage>();
			promptMessage.Add(new LlmMessage("system", $@"You are deciding what modules to use in the system. 

module is the module name
type is the module type that should be used
arguments should have the scheme: {{name:string, status:object}}
keep argument name simple as possible))
"));
			promptMessage.Add(new LlmMessage("user", content));

			var request = new LlmRequest("ModuleLoader", promptMessage);
			request.scheme = @"[{{module:string,
type:string,
arguments:object
}}]";

			return request;
		}

		public record Answer(string Module, string Type);



	}
}
