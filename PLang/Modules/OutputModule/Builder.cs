using Microsoft.Playwright;
using PLang.Building.Model;
using PLang.Errors.Builder;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.OutputStream.Messages;
using PLang.Utils;
using System.Xml.Linq;
using static PLang.Utils.StepHelper;

namespace PLang.Modules.OutputModule
{
	public class Builder : BaseBuilder
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly IEngine engine;

		public Builder(IPLangFileSystem fileSystem, IEngine engine)
		{
			this.fileSystem = fileSystem;
			this.engine = engine;
		}

		public async Task<(Instruction?, IBuilderError?)> BuilderAsk(GoalStep step, Instruction instruction, GenericFunction gf)
		{
			var askMessage = gf.GetParameter<AskMessage>("askMessage");

			if (PathHelper.IsTemplateFile(askMessage.Content))
			{
				var filePath = GetPath(askMessage.Content, step.Goal);
				if (!fileSystem.File.Exists(filePath) && !filePath.Contains("%"))
				{
					Dictionary<string, object?> parameters = new();
					parameters.Add("step", step);
					parameters.Add("goal", step.Goal);
					parameters.Add("instruction", instruction);
					parameters.Add("fileName", askMessage.Content);

					GoalToCallInfo goalToCallInfo = new GoalToCallInfo("/modules/UiModule/CreateTemplateFile", parameters);

					var (program, programError) = engine.Modules.Get<CallGoalModule.Program>();
					if (programError != null) return (instruction, new BuilderError(programError));
					var result = await program!.RunGoal(goalToCallInfo);
					if (result.Error != null) return (instruction, new BuilderError(result.Error));
				}
			}

			return (instruction, null);
		}
	}
}
