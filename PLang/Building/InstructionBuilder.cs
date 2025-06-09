
using Microsoft.Extensions.Logging;
using NBitcoin;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Interfaces;
using PLang.Modules;
using PLang.Runtime;
using PLang.Services.LlmService;
using PLang.Utils;
using RazorEngineCore;
using System.Reflection;
using System.Text.Json;
using static PLang.Modules.BaseBuilder;


namespace PLang.Building
{
	public interface IInstructionBuilder
	{
		Task<(Model.Instruction?, IBuilderError?)> BuildInstruction(StepBuilder stepBuilder, Goal goal, GoalStep step, IBuilderError? previousBuildError = null);
		Task<(Instruction Instruction, IBuilderError? Error)> RunBuilderMethod(GoalStep goalStep, Model.Instruction? instruction, IGenericFunction? gf);
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
		public Dictionary<string, List<IBuilderError>> ErrorCount { get; set; } = new();
		
		public async Task<(Model.Instruction?, IBuilderError?)> BuildInstruction(StepBuilder stepBuilder, Goal goal, GoalStep step, IBuilderError? previousBuildError = null)
		{
			var result = await BuildInstructionInternal(stepBuilder, goal, step, previousBuildError);
			if (result.Error == null) return result;

			if (previousBuildError != null) result.Error.ErrorChain.Add(previousBuildError);
			if (result.Error is IInvalidModuleError ime) return result;

			if (!result.Error.ContinueBuild || !result.Error.Retry) return result;
			if (result.Error.RetryCount < GetErrorCount(step, result.Error)) return (result.Instruction, FunctionCouldNotBeCreated(step));

			logger.LogWarning($"- Error building step, will try again. Error: {result.Error.Message}");
			return await BuildInstruction(stepBuilder, goal, step, result.Error);
		}


		private async Task<(Model.Instruction? Instruction, IBuilderError? Error)> BuildInstructionInternal(StepBuilder stepBuilder, Goal goal, GoalStep step, IBuilderError? previousBuildError = null)
		{
			var classInstance = builderFactory.Create(step.ModuleType);
			classInstance.InitBaseBuilder(step, fileSystem, llmServiceFactory, typeHelper, memoryStack, context, variableHelper, logger);

			logger.LogInformation($"- Build using {step.ModuleType}");
			
			var build = await classInstance.Build(step, previousBuildError);
			if (build.BuilderError != null) return build;

			if (build.Instruction == null || build.Instruction.Function == null)
			{
				return (null, new StepBuilderError($"Could not map {step.Text} to function. Refine your text", step));
			}

			var instruction = build.Instruction;

			(instruction, var builderError) = await RunBuilderMethod(step, instruction, instruction.Function);
			if (builderError != null) return (instruction, builderError);

			// ValidateFunctions run after builder methods since they might change the function
			var methodHelper = new MethodHelper(step, variableHelper, typeHelper);
			(var parameterProperties, var returnObjectsProperties, var invalidFunctionError) = methodHelper.ValidateFunctions(instruction.Function, step.ModuleType, memoryStack);
			if (invalidFunctionError != null)
			{
				if (previousBuildError?.ErrorChain.Count > 1)
				{
					return (build.Instruction, new InvalidModuleError(step.ModuleType, "Cannot validate function after 2 tries. Try another module.", instruction.Function));
				}
				return (build.Instruction, invalidFunctionError);
			}

			var error = await stepBuilder.LoadVariablesIntoMemoryStack(instruction.Function, memoryStack, context, settings);
			if (error != null) return (build.Instruction, error);


			//write properties of objects to instruction file
			instruction.Properties.AddOrReplace("Parameters", parameterProperties);
			instruction.Properties.AddOrReplace("ReturnValues", returnObjectsProperties);

			// since the no invalid function, we can save the instruction file
			WriteInstructionFile(step, instruction);
			return (instruction, null);
		}

		private int GetErrorCount(GoalStep step, IBuilderError error)
		{
			ErrorCount.TryGetValue(step.RelativePrPath, out List<IBuilderError>? errors);
			if (errors == null) errors = new();

			errors.Add(error);
			ErrorCount!.AddOrReplace(step.RelativePrPath, errors);
			return errors.Count;
			
		}

		private InstructionBuilderError FunctionCouldNotBeCreated(GoalStep step)
		{
			ErrorCount.TryGetValue(step.RelativePrPath, out var errors);
			string errorCount = "";
			if (errors != null && errors.Count > 0)
			{
				errorCount = $"I tried {errorCount} times.";
			}
			return new InstructionBuilderError($@"Could not create instruction for {step.Text}. {errorCount}", step, step.Instruction, 
				Retry: false,
				FixSuggestion:@$"Try defining the step in more detail.

You have 3 options:
	- Rewrite your step to fit better with a modules that you have installed. 
		How to write the step? Get help here https://github.com/PLangHQ/plang/blob/main/Documentation/modules/README.md
	- Install an App from that can handle your request and call that
	- Build your own module. This requires a C# developer knowledge

Builder will continue on other steps but not this one ({step.Text.MaxLength(30, "...")}).
");
		}
		private void WriteInstructionFile(GoalStep step, Model.Instruction instruction)
		{
			instruction.Reload = false;

			if (!fileSystem.Directory.Exists(step.Goal.AbsolutePrFolderPath))
			{
				fileSystem.Directory.CreateDirectory(step.Goal.AbsolutePrFolderPath);
			}

			fileSystem.File.WriteAllText(step.AbsolutePrFilePath, JsonConvert.SerializeObject(instruction, GoalSerializer.Settings));
		}


		public async Task<(Instruction Instruction, IBuilderError? Error)> RunBuilderMethod(GoalStep goalStep, Model.Instruction instruction, IGenericFunction gf)
		{
			var builder = typeHelper.GetBuilderType(goalStep.ModuleType);
			if (builder == null || gf == null) return (instruction, null);

			var defaultValidate = builder.GetMethod("BuilderValidate");

			var method = builder.GetMethod("Builder" + gf.Name);
			if (method == null && defaultValidate == null) return (instruction, null);

			var classInstance = builderFactory.Create(goalStep.ModuleType);
			classInstance.InitBaseBuilder(goalStep, fileSystem, llmServiceFactory, typeHelper, memoryStack, context, variableHelper, logger);

			if (method != null)
			{
				var method2 = classInstance.GetType().GetMethod("Builder" + gf.Name);
				if (method2 == null) return (instruction, null);

				var result = await InvokeMethod(classInstance, method2, goalStep, instruction!, gf);
				if (result.Error != null) return result;
				
				instruction = result.Instruction;
			}

			if (defaultValidate != null)
			{
				var method2 = classInstance.GetType().GetMethod("BuilderValidate");
				if (method2 != null)
				{
					var result = await InvokeMethod(classInstance, method2, goalStep, instruction!, gf);
					if (result.Error != null) return result;

					instruction = result.Instruction;
				}
			}

			return (instruction, null);
		}

		private async Task<(Instruction Instruction, IBuilderError? Error)> InvokeMethod(BaseBuilder classInstance, MethodInfo method, GoalStep goalStep, Model.Instruction instruction, IGenericFunction gf)
		{
			try
			{
				var result = method.Invoke(classInstance, [goalStep, instruction, gf]);
				if (result is not Task task)
				{
					return (instruction, null);
				}

				await task;

				Type taskType = task.GetType();
				var returnArguments = taskType.GetGenericArguments();
				if (returnArguments.Length == 0) return (instruction, null);

				if (returnArguments[0] == typeof(IBuilderError))
				{
					var resultTask = task as Task<IBuilderError?>;
					return (instruction, resultTask?.Result);
				}

				if (returnArguments[0] == typeof(Instruction))
				{
					var resultTask = task as Task<Instruction>;
					return (resultTask.Result, null);
				}

				if (returnArguments[0].Name.StartsWith("ValueTuple"))
				{
					var resultTask = task as Task<(Instruction, IBuilderError?)>;
					return resultTask.Result;
				}

				return (instruction, new StepBuilderError("I dont know how to handle return value." + returnArguments.GetType().FullName, goalStep));
			}
			catch (Exception ex)
			{
				return (instruction, new InstructionBuilderError($"Failed to invoke validation method: {goalStep.ModuleType}.{method.Name}", goalStep, instruction,
					"ValidationInvokeFailed", Exception: ex, FixSuggestion: $"Try rebuilding the .pr file: {goalStep.RelativePrPath}"));
			}
		}
	}


}
