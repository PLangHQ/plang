using System.ComponentModel;

namespace PLang.Modules.InjectModule
{
	[Description("Dependancy injection")]
	public class Program : BaseProgram
	{
		public Program() : base()
		{

		}

		[Description("type can be: db, settings, caching, logger, llm, askuser, encryption, archiver. Injection can be for runtime, builder or both.")]
		public async Task Inject(string type, string pathToDll, bool isDefaultOrGlobalForWholeApp = false, string? environmentVariable = "PLANG_ENV", string? environmentVariableValue = null)
		{
			base.RegisterForPLangUserInjections(type, pathToDll, (bool)isDefaultOrGlobalForWholeApp, environmentVariable, environmentVariableValue);
		
		}
	}
}
