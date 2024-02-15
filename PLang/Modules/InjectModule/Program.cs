using System.ComponentModel;

namespace PLang.Modules.InjectModule
{
	[Description("Dependancy injection")]
	public class Program : BaseProgram
	{
		public Program() : base()
		{

		}

		[Description("type can be: db, settings, caching, logger, llm, askuser, encryption, archiver")]
		public async Task Inject(string type, string pathToDll, bool isDefaultOrGlobalForWholeApp = false)
		{
			base.RegisterForPLangUserInjections(type, pathToDll, (bool)isDefaultOrGlobalForWholeApp);
		
		}
	}
}
