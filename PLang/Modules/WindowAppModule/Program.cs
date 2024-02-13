using PLang.Building.Parsers;
using PLang.Interfaces;
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

		[Description("goalName is required. It is one word. Example: call !NameOfGoal, run !Google.Search. Do not use the names in your response unless defined by user")]
		public async Task<string> RunWindowApp(string goalName, Dictionary<string, object?>? parameters = null, 
			int width = 800, int height = 450, string? iconPath = null, string windowTitle = "plang")
		{
			if (context.ContainsKey("__WindowApp__"))
			{
				var iform = context["__WindowApp__"] as IForm;
				if (iform != null)
				{
					iform.SetSize(width, height);
					if (iconPath != null) iform.SetIcon(iconPath);
					iform.SetTitle(windowTitle);
				}
			}
            await pseudoRuntime.RunGoal(engine, context, Path.DirectorySeparatorChar.ToString(), goalName, parameters, Goal);

			return "";
		}



	}

}

