using LightInject;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Modules;
using PLang.Runtime;
using PLang.Utils;
using static PLang.Modules.BaseBuilder;
using static PLang.Utils.MethodHelper;


namespace PLang.Building
{
    public interface IInstructionBuilder
	{
		Task BuildInstruction(StepBuilder stepBuilder, Goal goal, GoalStep goalStep, string module, int stepNr, List<string>? excludeModules = null, int errorCount = 0);
	}

	public class InstructionBuilder : IInstructionBuilder
	{
		private readonly ITypeHelper typeHelper;
		private readonly Lazy<ILlmService> llmService;
		private readonly IBuilderFactory builderFactory;
		private readonly ILogger logger;
		private readonly IPLangFileSystem fileSystem;
		private MemoryStack memoryStack;
		private readonly PLangAppContext context;
		private readonly VariableHelper variableHelper;
		private readonly MethodHelper methodHelper;

		public InstructionBuilder(ILogger logger, IPLangFileSystem fileSystem, ITypeHelper typeHelper,
			Lazy<ILlmService> llmService, IBuilderFactory builderFactory, 
			MemoryStack memoryStack, PLangAppContext context, VariableHelper variableHelper)
		{
			this.typeHelper = typeHelper;
			this.llmService = llmService;
			this.builderFactory = builderFactory;
			this.logger = logger;
			this.fileSystem = fileSystem;
			this.memoryStack = memoryStack;
			this.context = context;
			this.variableHelper = variableHelper;
			
		}

		public async Task BuildInstruction(StepBuilder stepBuilder, Goal goal, GoalStep step, string module, int stepIndex, List<string>? excludeModules = null, int errorCount = 0)
		{
			var classInstance = builderFactory.Create(module);
			classInstance.InitBaseBuilder(module, fileSystem, llmService.Value, typeHelper, memoryStack, context, variableHelper, logger);

			logger.LogDebug($"- Build using {module}");

			var instruction = await classInstance.Build(step);
			instruction.Text = step.Text;

			if (instruction.Action == null)
			{
				throw new BuilderException($"Could not map {step.Text} to function. Refined your text");
			}

			var functions = instruction.GetFunctions();
			var invalidFunctions = ValidateFunctions(step, functions, module);
			if (invalidFunctions.Count > 0)
			{
				foreach (var invalidFunction in invalidFunctions) {
					logger.LogWarning(invalidFunction.explain);
				}
				await Retry(stepBuilder, invalidFunctions, module, goal, stepIndex, excludeModules, errorCount);
				return;
			}
			foreach (var function in functions)
			{
				if (function != null && function.ReturnValue != null && function.ReturnValue.Count > 0)
				{
					foreach (var returnValue in function.ReturnValue)
					{
						memoryStack.PutForBuilder(returnValue.VariableName, returnValue.Type);
					}
				}
			}
			// since the no invalid function, we can save the instruction file
			WriteInstructionFile(step, instruction);

		}
		private async Task Retry(StepBuilder stepBuilder, List<InvalidFunction> invalidFunctions, string module, Goal goal, int stepIndex, List<string>? excludeModules, int errorCount)
		{
			errorCount++; //always increase the errorCount to prevent endless requests

			if (excludeModules == null) excludeModules = new List<string>();
			if (excludeModules.Count < 3)
			{
				if (invalidFunctions.FirstOrDefault(p => p.excludeModule) == null && errorCount < 2)
				{
					await BuildInstruction(stepBuilder, goal, goal.GoalSteps[stepIndex], module, stepIndex, excludeModules, errorCount);
				}
				else
				{
					excludeModules.Add(module);
					await stepBuilder.BuildStep(goal, stepIndex, excludeModules, errorCount);
				}
				return;
			}

			logger.LogWarning($@"Could not find correct module for step:{goal.GoalSteps[stepIndex].Text}. I tried following modules:{string.Join(",", excludeModules)}.
You have 3 options.
- Rewrite your step to fit better with a modules that you have installed
- Install an App from that can handle your request and call that
- Build your own module. This requires a C# developer knowledge
");
			return;
		}

	
		public List<InvalidFunction> ValidateFunctions(GoalStep step, GenericFunction[] functions, string module)
		{
			List<InvalidFunction> invalidFunctions = new List<InvalidFunction>();
			if (functions == null || functions[0] == null) return invalidFunctions;

			var methodHelper = new MethodHelper(step, variableHelper, typeHelper, llmService.Value);
			return methodHelper.ValidateFunctions(functions, module, memoryStack);
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
