using LightInject;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Modules;
using PLang.Runtime;
using PLang.Services.LlmService;
using PLang.Utils;
using static PLang.Modules.BaseBuilder;
using static PLang.Utils.MethodHelper;


namespace PLang.Building
{
	public interface IInstructionBuilder
	{
		Task<IBuilderError?> BuildInstruction(StepBuilder stepBuilder, Goal goal, GoalStep goalStep, string module, int stepNr, List<string>? excludeModules = null, int errorCount = 0);
	}

	public class InstructionBuilder : IInstructionBuilder
	{
		private readonly ITypeHelper typeHelper;
		private readonly ILlmServiceFactory llmServiceFactory;
		private readonly IBuilderFactory builderFactory;
		private readonly ILogger logger;
		private readonly IPLangFileSystem fileSystem;
		private MemoryStack memoryStack;
		private readonly PLangAppContext context;
		private readonly VariableHelper variableHelper;
		private readonly ISettings settings;

		public InstructionBuilder(ILogger logger, IPLangFileSystem fileSystem, ITypeHelper typeHelper,
			ILlmServiceFactory llmServiceFactory, IBuilderFactory builderFactory,
			MemoryStack memoryStack, PLangAppContext context, VariableHelper variableHelper, ISettings settings)
		{
			this.typeHelper = typeHelper;
			this.llmServiceFactory = llmServiceFactory;
			this.builderFactory = builderFactory;
			this.logger = logger;
			this.fileSystem = fileSystem;
			this.memoryStack = memoryStack;
			this.context = context;
			this.variableHelper = variableHelper;
			this.settings = settings;
		}

		public async Task<IBuilderError?> BuildInstruction(StepBuilder stepBuilder, Goal goal, GoalStep step, string module, int stepIndex, List<string>? excludeModules = null, int errorCount = 0)
		{
			var classInstance = builderFactory.Create(module);
			classInstance.InitBaseBuilder(module, fileSystem, llmServiceFactory, typeHelper, memoryStack, context, variableHelper, logger);

			logger.LogInformation($"- Build using {module}");

			var build = await classInstance.Build(step);
			if (build.BuilderError != null || build.Instruction == null || build.Instruction.Action == null)
			{
				return build.BuilderError ?? new InstructionBuilderError($"Could not map {step.Text} to function. Refine your text", step);
			}

			var instruction = build.Instruction;
			instruction.Text = step.Text;
			var functions = instruction.GetFunctions();
			var methodHelper = new MethodHelper(step, variableHelper, memoryStack, typeHelper, llmServiceFactory);
			var invalidFunctionError = methodHelper.ValidateFunctions(functions, module, memoryStack);

			if (invalidFunctionError != null)
			{
				return await Retry(stepBuilder, invalidFunctionError, module, goal, step, stepIndex, excludeModules, errorCount);
			}
			foreach (var function in functions)
			{
				StepBuilder.LoadVariablesIntoMemoryStack(function, memoryStack, context, settings);
			}


			// since the no invalid function, we can save the instruction file
			WriteInstructionFile(step, instruction);
			return null;
		}
		private async Task<IBuilderError?> Retry(StepBuilder stepBuilder, MultipleBuildError invalidFunctionError, string module, Goal goal, GoalStep step, int stepIndex, List<string>? excludeModules, int errorCount)
		{
			errorCount++; //always increase the errorCount to prevent endless requests

			if (excludeModules == null) excludeModules = new List<string>();
			if (excludeModules.Count < 3)
			{
				if (invalidFunctionError.Errors.FirstOrDefault(p => ((InvalidFunctionsError)p).ExcludeModule) == null && errorCount < 2)
				{
					return await BuildInstruction(stepBuilder, goal, goal.GoalSteps[stepIndex], module, stepIndex, excludeModules, errorCount);
				}
				else
				{
					excludeModules.Add(module);
					return await stepBuilder.BuildStep(goal, stepIndex, excludeModules, errorCount);
				}
			}

			return new InstructionBuilderError($@"Could not find module for {step.Text}. 
Try defining the step in more detail.

You have 3 options:
	- Rewrite your step to fit better with a modules that you have installed. 
		How to write the step? Get help here https://github.com/PLangHQ/plang/blob/main/Documentation/modules/README.md
	- Install an App from that can handle your request and call that
	- Build your own module. This requires a C# developer knowledge

Builder will continue on other steps but not this one ({step.Text}).
", step);
		}


		private void WriteInstructionFile(GoalStep step, Model.Instruction? instructions)
		{
			if (instructions == null) return;

			instructions.Reload = false;

			if (!fileSystem.Directory.Exists(step.Goal.AbsolutePrFolderPath))
			{
				fileSystem.Directory.CreateDirectory(step.Goal.AbsolutePrFolderPath);
			}
			fileSystem.File.WriteAllText(step.AbsolutePrFilePath, JsonConvert.SerializeObject(instructions, Formatting.Indented));
		}
	}


}
