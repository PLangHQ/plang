
using Jil;
using Microsoft.Extensions.Logging;
using Namotion.Reflection;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Interfaces;
using PLang.Models;
using PLang.Modules;
using PLang.Runtime;
using PLang.Services.LlmService;
using PLang.Utils;
using RazorEngineCore;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using static PLang.Modules.BaseBuilder;


namespace PLang.Building
{
	public interface IInstructionBuilder
	{
		Task<(Model.Instruction?, IBuilderError?)> BuildInstruction(StepBuilder stepBuilder, Goal goal, GoalStep step, IBuilderError? previousBuildError = null);
		Task<(Instruction Instruction, IBuilderError? Error)> RunBuilderMethod(GoalStep goalStep, Model.Instruction? instruction, IGenericFunction? gf);
		Task<(Instruction Instruction, IBuilderError? Error)> RunStepValidation(GoalStep step, Instruction instruction, IGenericFunction gf);
		Task<(Instruction Instruction, IBuilderError? Error)> ValidateGoalToCall(GoalStep goalStep, Instruction instruction);
	}

	public class InstructionBuilder : IInstructionBuilder
	{
		private readonly ITypeHelper typeHelper;
		private readonly ILlmServiceFactory llmServiceFactory;
		private readonly IBuilderFactory builderFactory;
		private readonly ILogger logger;
		private readonly IPLangFileSystem fileSystem;
		private MemoryStack memoryStack;
		private readonly PLangContext context;
		private readonly VariableHelper variableHelper;
		private readonly ISettings settings;
		private readonly ProgramFactory programFactory;
		private readonly IGoalParser goalParser;
		private readonly PrParser prParser;
		private readonly MethodHelper methodHelper;

		public InstructionBuilder(ILogger logger, IPLangFileSystem fileSystem, ITypeHelper typeHelper,
			ILlmServiceFactory llmServiceFactory, IBuilderFactory builderFactory,
			IMemoryStackAccessor memoryStackAccessor, IPLangContextAccessor contextAccessor, VariableHelper variableHelper, ISettings settings,
			ProgramFactory programFactory, IGoalParser goalParser, PrParser prParser, MethodHelper methodHelper)
		{
			this.typeHelper = typeHelper;
			this.llmServiceFactory = llmServiceFactory;
			this.builderFactory = builderFactory;
			this.logger = logger;
			this.fileSystem = fileSystem;
			this.memoryStack = memoryStackAccessor.Current;
			this.context = contextAccessor.Current;
			this.variableHelper = variableHelper;
			this.settings = settings;
			this.programFactory = programFactory;
			this.goalParser = goalParser;
			this.prParser = prParser;
			this.methodHelper = methodHelper;
		}
		public Dictionary<string, List<IBuilderError>> ErrorCount { get; set; } = new();

		public async Task<(Model.Instruction?, IBuilderError?)> BuildInstruction(StepBuilder stepBuilder, Goal goal, GoalStep step, IBuilderError? previousBuildError = null)
		{
			try
			{
				var result = await BuildInstructionInternal(stepBuilder, goal, step, previousBuildError);
				if (result.Error == null)
				{
					if (previousBuildError != null)
					{
						logger.LogInformation("  - 👍 Error has been fixed");
					}
					return result;
				}

				if (previousBuildError != null) result.Error.ErrorChain.Add(previousBuildError);
				if (result.Error is IInvalidModuleError ime) return result;

				if (!result.Error.ContinueBuild || !result.Error.Retry) return result;
				if (result.Error.RetryCount < GetErrorCount(step, result.Error)) return (result.Instruction, FunctionCouldNotBeCreated(step));

				logger.LogWarning($"- Error building step, will try again. Error: {result.Error.Message}");
				return await BuildInstruction(stepBuilder, goal, step, result.Error);
			}
			catch (Exception ex2)
			{
				string? innerMessage = ex2.InnerException?.Message;
				if (ex2.InnerException?.InnerException != null)
				{
					innerMessage = ex2.InnerException?.InnerException.Message;
				}
				string error = $@"
<error>
{innerMessage}
{ex2.Message}
<error>

";
				if (step.Instruction?.LlmRequest != null)
				{
					foreach (var llmRequest in step.Instruction.LlmRequest)
					{
						error += $@"<llm_response>
{llmRequest.RawResponse}
<llm_response>";
					}
				}

				return (null, new StepBuilderError(error


, step, ex: ex2, Retry: false, ContinueBuild: false));
			}
		}


		private async Task<(Model.Instruction? Instruction, IBuilderError? Error)> BuildInstructionInternal(StepBuilder stepBuilder, Goal goal, GoalStep step, IBuilderError? previousBuildError = null)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			logger.LogDebug("Building instruction");
			var classInstance = builderFactory.Create(step.ModuleType);
			classInstance.InitBaseBuilder(step, fileSystem, llmServiceFactory, typeHelper, memoryStack, context, variableHelper, logger);

			string logInfo = (previousBuildError != null) ? "Retrying to build" : "Build";
			logger.LogInformation(@$"  - {logInfo} using {step.ModuleType}");

			var build = await classInstance.Build(step, previousBuildError);
			if (build.BuilderError != null) return build;

			if (build.Instruction == null || build.Instruction.Function == null)
			{
				return (null, new StepBuilderError($"Could not map {step.Text} to function. Refine your text", step));
			}

			var instruction = build.Instruction;
			step.Instruction = instruction;

			logger.LogDebug($"Done with instruction - running Builder methods - {stopwatch.ElapsedMilliseconds} ");
			(instruction, var builderError) = await RunStepValidation(step, instruction, instruction.Function);
			if (builderError != null) return (instruction, builderError);

			logger.LogDebug($"Done with builder method - running validate functions - {stopwatch.ElapsedMilliseconds} ");
			// ValidateFunctions run after builder methods since they might change the function

			(var parameterProperties, var returnObjectsProperties, var invalidFunctionError) = methodHelper.ValidateFunctions(step, memoryStack);
			if (invalidFunctionError != null)
			{
				if (previousBuildError?.ErrorChain.Count > 1)
				{
					return (build.Instruction, new InvalidModuleError(step.ModuleType, "Cannot validate function after 2 tries. Try another module.", instruction.Function));
				}
				return (build.Instruction, invalidFunctionError);
			}
			logger.LogDebug($"Done with function validation - putting into memory and writing to file - {stopwatch.ElapsedMilliseconds} ");
			var error = await stepBuilder.LoadVariablesIntoMemoryStack(instruction.Function, memoryStack, settings);
			if (error != null) return (build.Instruction, error);

			instruction.ModuleType = step.ModuleType;
			//write properties of objects to instruction file
			instruction.Properties.AddOrReplace("Parameters", parameterProperties);
			instruction.Properties.AddOrReplace("ReturnValues", returnObjectsProperties);

			// since the no invalid function, we can save the instruction file
			WriteInstructionFile(step, instruction);
			logger.LogDebug($"Done with instruction building total time: {stopwatch.ElapsedMilliseconds} ");
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
				errorCount = $"I tried {errors.Count} times.";
			}
			return new InstructionBuilderError($@"Could not create instruction for {step.Text}. {errorCount}", step, step.Instruction,
				Retry: false,
				FixSuggestion: @$"Try defining the step in more detail.

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


		public async Task<(Instruction Instruction, IBuilderError? Error)> RunStepValidation(GoalStep step, Model.Instruction instruction, IGenericFunction gf)
		{
			if (HasNoValidation()) return (instruction, null);

			(instruction, var builderError) = await RunBuilderMethod(step, instruction, instruction.Function);
			if (builderError != null) return (instruction, builderError);
			
			(instruction, var error) = await ValidateGoalToCall(step, instruction);
			if (error != null) return (instruction, new BuilderError(error) { Retry = false });
			
			return (instruction, null);
		}
		public bool HasNoValidation()
		{
			var obj = AppContext.GetData(ReservedKeywords.ParametersAtAppStart);
			if (obj != null && obj is string[] args)
			{
				if (args.Any(p => p.Equals("--novalidation"))) return true;
			}
			return false;
		}
		public async Task<(Instruction Instruction, IBuilderError? Error)> RunBuilderMethod(GoalStep goalStep, Model.Instruction instruction, IGenericFunction gf)
		{
			

			Stopwatch stopwatch = Stopwatch.StartNew();
			logger.LogDebug($"    - Running 'Builder{gf.Name}' - {stopwatch.ElapsedMilliseconds}");

			var builder = typeHelper.GetBuilderType(goalStep.ModuleType);
			if (builder == null || gf == null) return (instruction, null);

			logger.LogDebug($"    - Have builder type '{goalStep.ModuleType}' - {stopwatch.ElapsedMilliseconds}");
			
			var isValidatedMethod = builder.GetMethod("IsValidated");			
			var defaultValidate = builder.GetMethod("BuilderValidate");
			var method = builder.GetMethod("Builder" + gf.Name);

			if (method == null && defaultValidate == null && isValidatedMethod == null) return (instruction, null);

			logger.LogDebug($"    - Create instance of {goalStep.ModuleType} - {stopwatch.ElapsedMilliseconds}");
			var classInstance = builderFactory.Create(goalStep.ModuleType);
			logger.LogDebug($"    - Have instance of {goalStep.ModuleType} - {stopwatch.ElapsedMilliseconds}");

			classInstance.InitBaseBuilder(goalStep, fileSystem, llmServiceFactory, typeHelper, memoryStack, context, variableHelper, logger);

			if (isValidatedMethod != null)
			{
				var method2 = classInstance.GetType().GetMethod("IsValidated");
				if (method2 != null)
				{
					var result = method2.Invoke(classInstance, [goalStep, instruction!, gf]);
					if (result is bool b && b)
					{
						return (instruction, null);
					}
				}
			}

			logger.LogDebug($"    - Loaded '{goalStep.ModuleType}' - {stopwatch.ElapsedMilliseconds}");
			if (method != null)
			{
				logger.LogDebug($"    - Running 'Builder{gf.Name}' - {stopwatch.ElapsedMilliseconds}");

				var method2 = classInstance.GetType().GetMethod("Builder" + gf.Name);
				if (method2 == null) return (instruction, null);
				var result = await InvokeMethod(classInstance, method2, goalStep, instruction!, gf);

				logger.LogDebug($"    - Done 'Builder{gf.Name}' - {stopwatch.ElapsedMilliseconds}");

				if (result.Error != null) return result;

				instruction = result.Instruction;
			}

			if (defaultValidate != null)
			{
				logger.LogDebug($"    - Running 'BuilderValidate' - {stopwatch.ElapsedMilliseconds}");
				var method2 = classInstance.GetType().GetMethod("BuilderValidate");
				if (method2 != null)
				{
					var result = await InvokeMethod(classInstance, method2, goalStep, instruction!, gf);
					if (result.Error != null) return result;

					instruction = result.Instruction;
				}
				logger.LogDebug($"    - Done 'BuilderValidate' - {stopwatch.ElapsedMilliseconds}");
			}


			var setAsValidatedMethod = builder.GetMethod("SetAsValidated");
			if (setAsValidatedMethod != null)
			{
				logger.LogDebug($"    - Running 'SetAsValidated' - {stopwatch.ElapsedMilliseconds}");
				var method2 = classInstance.GetType().GetMethod("SetAsValidated");
				if (method2 != null)
				{
					var result = await InvokeMethod(classInstance, method2, goalStep, instruction!, gf);
					if (result.Error != null) return result;

					instruction = result.Instruction;
					if (instruction != null)
					{
						WriteInstructionFile(goalStep, instruction);
					}
				}
				logger.LogDebug($"    - Done 'SetAsValidated' - {stopwatch.ElapsedMilliseconds}");
			}
			/*
			(instruction, var error) = await ValidateGoalToCall(goalStep, instruction);
			if (error != null) return (instruction, new BuilderError(error) { Retry = false });
			*/
				return (instruction, null);
		}

		public async Task<(Instruction Instruction, IBuilderError? Error)> ValidateGoalToCall(GoalStep goalStep, Instruction instruction)
		{
			var token = instruction.FunctionJson;

			var nodes = JsonHelper.FindTokens(token, "Type", "PLang.Models.GoalToCallInfo", true);
			if (!nodes.Any()) return (instruction, null);
			foreach (var jsonNode in nodes)
			{
				if (jsonNode["Value"]?.Type == JTokenType.Null)
				{
					continue;
				}

				var goalToCall = jsonNode["Value"]!.ToObject<GoalToCallInfo>();
				if (goalToCall == null)
				{
					throw new Exception($"Expected value to be GoalToCallInfo. {ErrorReporting.CreateIssueShouldNotHappen}");
				}

				if (goalToCall.Name.Contains("%"))
				{
					logger.LogInformation($"Cannot validate goal to call that is dynamic: {goalToCall.Name} - {goalStep.RelativeGoalPath}:{goalStep.LineNumber}");
					return (instruction, null);
				}

				(var goalFound, var error) = GoalHelper.GetGoalPath(goalStep, goalToCall, goalParser.GetGoals(), prParser.GetSystemGoals());
				if (error != null) return (instruction, new BuilderError(error) { Retry = false });
				if (goalFound != null)
				{
					//if (goalToCall.Path?.Equals(goalFound.RelativeGoalPath) == true) return (instruction, null);

					goalToCall.Path = goalFound.RelativePrPath;
					jsonNode["Value"] = JToken.FromObject(goalToCall);
				}

			}

			instruction.FunctionJson = token;

			WriteInstructionFile(goalStep, instruction);

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
