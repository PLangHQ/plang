using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Utils;

namespace PLang.Modules.WebserverModule

{
	public class Builder : BaseBuilder
	{
		private readonly ILogger logger;
		private readonly ITypeHelper typeHelper;

		public Builder(ILogger logger, ITypeHelper typeHelper) : base()
		{
			this.logger = logger;
			this.typeHelper = typeHelper;
		}


		public override async Task<(Instruction? Instruction, IBuilderError? BuilderError)> Build(GoalStep step, IBuilderError? previousBuildError = null)
		{
			var buildResult = await base.Build(step, previousBuildError);
			if (buildResult.BuilderError != null) return buildResult;

			if (buildResult.Instruction.Function.Name == "AddRoute")
			{
				return await BuildFunction(step, buildResult.Instruction);
			}

			return buildResult;
			
		}

		private async Task<(Instruction? Instruction, IBuilderError? BuilderError)> BuildFunction(GoalStep step, Instruction instruction)
		{
			var programType = typeHelper.GetRuntimeType(step.ModuleType);
			if (programType == null) return (null, new InstructionBuilderError($"Could not load type {step.ModuleType}", step, instruction, Retry: false));

			var variables = GetVariablesInStep(step).Replace("%", "");

			var classDescriptionHelper = new ClassDescriptionHelper();
			var (classDescription, error) = classDescriptionHelper.GetClassDescription(programType, [instruction.Function.Name]);
			if (error != null) return (null, error);

			
			if (classDescription == null) return (null, new InstructionBuilderError($"Could not load method information for {instruction.Function.Name}", step, instruction, Retry: false));
			
			var methodJson = JsonConvert.SerializeObject(classDescription, new JsonSerializerSettings
			{
				NullValueHandling = NullValueHandling.Ignore
			});

			SetAssistant($@"
## functions details ##
{methodJson}
## functions details ##");

			var instructionResult = await base.Build(step);

			return instructionResult;


		}
	}

}

