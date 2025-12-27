using PLang.Building.Parsers;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.OutputStream;
using PLang.Services.OutputStream.Sinks;
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
		public async Task<IError?> RunWindowApp(GoalToCallInfo goalName, 
			int width = 800, int height = 450, string? iconPath = null, string windowTitle = "plang")
		{
			var sink = context.Output.GetActor("user").GetChannel().Sink;
			if (sink is not AppOutputSink os)
			{
				return new StepError("This is not UI Output, did you run plang instead of plangw?", goalStep);
			}

			os.IForm.SetTitle(windowTitle);
			os.IForm.SetSize(width, height);
			if (iconPath != null) os.IForm.SetIcon(iconPath);
			
			var result = await pseudoRuntime.RunGoal(engine, contextAccessor, Path.DirectorySeparatorChar.ToString(), goalName, Goal);
			//((UIOutputStream)os).IForm.Flush(result.output.Data.ToString());
			os.IForm.Visible = true;
			return result.Error;
		}



	}

}

