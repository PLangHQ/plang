using PLang.Building.Parsers;
using PLang.Runtime;
using System.ComponentModel;

namespace PLang.Modules.WindowAppModule
{

	public class Program : BaseProgram
	{
		private readonly IPseudoRuntime pseudoRuntime;
		private readonly IEngine engine;

		public Program(IPseudoRuntime pseudoRuntime, IEngine engine) : base()
		{
			this.pseudoRuntime = pseudoRuntime;
			this.engine = engine;
		}

		[Description("goalName is required. It is one word. Example: !CallGoal, or !Google.Search. Do not use the names in your response unless defined by user")]
		public async Task<string> RunWindowApp(string goalName, Dictionary<string, object>? parameters = null)
		{
			
			await pseudoRuntime.RunGoal(engine, context, "", goalName, parameters, Goal);

			return "";
		}

	}

}

