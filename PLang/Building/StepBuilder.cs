using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Building.Events;
using PLang.Building.Model;
using PLang.Exceptions;
using PLang.Exceptions.Handlers;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.CompilerService;
using PLang.Utils;
using System.Diagnostics;
using System.Text.RegularExpressions;
using static PLang.Modules.BaseBuilder;
using static PLang.Services.CompilerService.CSharpCompiler;

namespace PLang.Building
{
	public interface IStepBuilder
	{
		Task BuildStep(Goal goal, int stepNr, List<string>? excludeModules = null, int errorCount = 0);
	}

	public class StepBuilder : IStepBuilder
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly Lazy<ILlmService> aiService;
		private readonly Lazy<ILogger> logger;
		private readonly IInstructionBuilder instructionBuilder;
		private readonly IEventRuntime eventRuntime;
		private readonly ITypeHelper typeHelper;
		private readonly IErrorHelper errorHelper;
		private readonly MemoryStack memoryStack;
		private readonly VariableHelper variableHelper;
		private readonly IExceptionHandler exceptionHandler;

		public StepBuilder(Lazy<ILogger> logger, IPLangFileSystem fileSystem, Lazy<ILlmService> aiService,
					IInstructionBuilder instructionBuilder, IEventRuntime eventRuntime, ITypeHelper typeHelper,
					IErrorHelper errorHelper, MemoryStack memoryStack, VariableHelper variableHelper, IExceptionHandler exceptionHandler)
		{
			this.fileSystem = fileSystem;
			this.aiService = aiService;
			this.logger = logger;
			this.instructionBuilder = instructionBuilder;
			this.eventRuntime = eventRuntime;
			this.typeHelper = typeHelper;
			this.errorHelper = errorHelper;
			this.memoryStack = memoryStack;
			this.variableHelper = variableHelper;
			this.exceptionHandler = exceptionHandler;
		}

		public async Task BuildStep(Goal goal, int stepIndex, List<string>? excludeModules = null, int errorCount = 0)
		{
			if (excludeModules == null) { excludeModules = new List<string>(); }

			var step = goal.GoalSteps[stepIndex];
			try
			{
				var strStepNr = (stepIndex + 1).ToString().PadLeft(2, '0');
				if (StepHasBeenBuild(step, stepIndex, excludeModules)) return;

				await eventRuntime.RunBuildStepEvents(EventType.Before, goal, step, stepIndex);

				LlmRequest llmQuestion = GetBuildStepQuestion(goal, step, excludeModules);

				logger.Value.LogDebug($"- Find module for {step.Text}");
				llmQuestion.Reload = false;
				var stepAnswer = await aiService.Value.Query<StepAnswer>(llmQuestion);
				if (stepAnswer == null)
				{
					if (errorCount > 2)
					{
						logger.Value.LogError($"Could not get answer from LLM. Will NOT try again. Tried {errorCount} times. Will continue to build next step.");
						return;
					}

					logger.Value.LogWarning($"Could not get answer from LLM. Will try again. This is attempt nr {++errorCount}");
					await BuildStep(goal, stepIndex, excludeModules, errorCount);
					return;
				}

				var module = stepAnswer.Modules.FirstOrDefault();
				if (module == null || module == "N/A")
				{
					logger.Value.LogError($"Could not find module for {step.Text}. Try defining the step in more detail.");
					logger.Value.LogWarning($@"You have 3 options.
- Rewrite your step to fit better with a modules that you have installed
- Install an App from that can handle your request and call that
- Build your own module. This requires a C# developer knowledge");
					logger.Value.LogError($"Builder will continue on other steps but not this one ({step.Text}).");

					return;
				}

				step.ModuleType = module;
				step.ErrorHandler = stepAnswer.ErrorHandler;
				step.WaitForExecution = stepAnswer.WaitForExecution;
				step.RetryHandler = stepAnswer.RetryHandler;
				step.CacheHandler = (module == "PLang.Modules.CachingModule") ? null : stepAnswer.CachingHandler;
				step.Name = stepAnswer.StepName;
				step.Description = stepAnswer.StepDescription;
				step.PrFileName = strStepNr + ". " + step.Name + ".pr";
				step.AbsolutePrFilePath = Path.Join(goal.AbsolutePrFolderPath, step.PrFileName);
				step.RelativePrPath = Path.Join(goal.RelativePrFolderPath, step.PrFileName);
				step.LlmRequest = llmQuestion;
				step.Number = stepIndex;
				if (goal.GoalSteps.Count > stepIndex + 1)
				{
					step.NextStep = goal.GoalSteps[stepIndex + 1];
				}

				step.RunOnce = (goal.RelativePrFolderPath.ToLower().Contains(".build" + Path.DirectorySeparatorChar + "setup"));
				try
				{
					await instructionBuilder.BuildInstruction(this, goal, step, module, stepIndex, excludeModules, errorCount);

					//Set reload after Build Instruction
					step.Reload = false;
					step.Generated = DateTime.Now;

					await eventRuntime.RunBuildStepEvents(EventType.After, goal, step, stepIndex);
				}
				catch (FunctionNotFoundException ffe)
				{
					excludeModules.Add(ffe.Message);
					await BuildStep(goal, stepIndex, excludeModules, ++errorCount);
				}
				catch (SkipStepException) { }

			}
			catch (StopBuilderException) { throw; }
			catch (Exception ex)
			{
				if (await exceptionHandler.Handle(ex, 500, "error", ex.Message))
				{
					await BuildStep(goal, stepIndex, excludeModules, errorCount);
				}
				//await errorHelper.ShowFriendlyErrorMessage(ex, step);
			}

		}


		private bool StepHasBeenBuild(GoalStep step, int stepIndex, List<string> excludeModules)
		{
			if (step.Number != stepIndex) return false;
			if (step.PrFileName == null || excludeModules.Count > 0) return false;

			if (!fileSystem.File.Exists(step.AbsolutePrFilePath))
			{
				return false;
			}
			var instruction = JsonHelper.ParseFilePath<Model.Instruction>(fileSystem, step.AbsolutePrFilePath);
			if (instruction == null) return false;

			step.Reload = (step.Reload || instruction.Reload && step.Text != instruction?.Text);
			if (step.Reload) return step.Reload;

			string? action = instruction?.Action?.ToString();
			if (action == null) return false;

			// lets load the return value into memoryStack
			if (action.Contains("ReturnValue"))
			{
				var gf = JsonConvert.DeserializeObject<GenericFunction>(action);
				LoadVariablesIntoMemoryStack(gf, memoryStack);
			}
			else if (action.Contains("OutParameterDefinition"))
			{
				var implementation = JsonConvert.DeserializeObject<Implementation>(action);
				if (implementation != null && implementation.OutParameterDefinition != null)
				{
					foreach (var vars in implementation.OutParameterDefinition)
					{
						memoryStack.PutForBuilder(vars.Key, JsonConvert.SerializeObject(vars.Value));
					}
				}
			}

			logger.Value.LogDebug($"- Step {step.Name} is already built");
			return true;
		}

		public static void LoadVariablesIntoMemoryStack(GenericFunction? gf, MemoryStack memoryStack)
		{
			if (gf == null) return;

			if (gf.ReturnValue != null && gf.ReturnValue.Count > 0)
			{
				foreach (var returnValue in gf.ReturnValue)
				{
					memoryStack.PutForBuilder(returnValue.VariableName, returnValue.Type);
				}
			}

			LoadParameters(gf, memoryStack);
		}

		private static void LoadParameters(GenericFunction? gf, MemoryStack memoryStack)
		{
			// todo: hack for now, should be able to load dynamically variables that are being set at build time
			// might have to structure the build
			if (gf == null || gf.Parameters == null || gf.Parameters.Count == 0) return;

			foreach (var parameter in gf.Parameters)
			{
				if (VariableHelper.IsVariable(parameter.Value))
				{
					memoryStack.PutForBuilder(parameter.Name, parameter.Type);
				}
			}

			if (gf.FunctionName == "RunGoal")
			{
				var json = gf.Parameters.FirstOrDefault(p => p.Name == "parameters")?.Value;
				if (json == null) return;
				var parameters = JsonConvert.DeserializeObject<Dictionary<string, object>>(json.ToString());
				if (parameters == null) return;

				foreach (var parameter in parameters)
				{
					memoryStack.PutForBuilder(parameter.Key, parameter.Value);
				}
			}



		}

		private LlmRequest GetBuildStepQuestion(Goal goal, GoalStep step, List<string>? excludeModules = null)
		{
			// user might define in his step specific module.

			var modulesAvailable = typeHelper.GetModulesAsString(excludeModules);
			var userRequestedModule = GetUserRequestedModule(step);
			if (userRequestedModule.Count > 0)
			{
				modulesAvailable = string.Join(", ", userRequestedModule);
			}
			var jsonScheme = TypeHelper.GetJsonSchema(typeof(StepAnswer));
			var system =
$@"You are provided with a statement from the user. 
This statement is a step in a Function. 

You MUST determine which module can be used to solve the statement.
You MUST choose from available modules provided by the assistant to determine which module. If you cannot choose module, set N/A

variable is defined with starting and ending %, e.g. %filePath%
! defines a call to a function

Modules: Name of module. Suggest 1-3 modules that could be used to solve the step.
StepName: Short name for step
StepDescription: Rewrite the step as you understand it, make it detailed
WaitForExecution: Indicates if code should wait for execution to finish, default is true
ErrorHandler: How to handle errors, default is null. if error should be handled but text (OnExceptionContainingTextCallGoal) is not defined, then use * for key
RetryHandler: How to retry the step if there is error, default is null
CachingHandler: How caching is handled, default is null
Read the description of each module, then determine which module to use

Be Concise
";
			if (step.Goal.RelativeGoalFolderPath.ToLower() == Path.DirectorySeparatorChar + "ui")
			{
				system += "\n\nUser is programming in the UI folder, this put priority on PLang.Modules.UiModule. The user is trying to create UI";
			}

				var question = step.Text;
			var assistant = $@"This is a list of modules you can choose from
## modules available starts ##
{modulesAvailable}
## modules available ends ##
## CachingType int ##
Sliding = 0, Absolute = 1
## CachingType int ##
";
			string variablesInStep = GetVariablesInStep(step);
			if (!string.IsNullOrEmpty(variablesInStep))
			{
				assistant += $@"
## variables available ##
{variablesInStep}
## variables available ##
";
			}

			List<LlmMessage> promptMessage = new();
			promptMessage.Add(new LlmMessage("system", system));
			promptMessage.Add(new LlmMessage("assistant", assistant));
			promptMessage.Add(new LlmMessage("user", question));

			var llmRequest = new LlmRequest("StepBuilder", promptMessage);
			llmRequest.scheme = jsonScheme;

			if (step.PrFileName == null || (excludeModules != null && excludeModules.Count > 0)) llmRequest.Reload = true;
			return llmRequest;
		}

		protected string GetVariablesInStep(GoalStep step)
		{
			var variables = variableHelper.GetVariables(step.Text);
			string vars = "";
			foreach (var variable in variables)
			{
				var objectValue = memoryStack.GetObjectValue(variable.OriginalKey, false);
				if (objectValue.Initiated)
				{
					vars += variable.OriginalKey + "(" + objectValue.Value + "), ";
				}
			}
			return vars;
		}

		private List<string> GetUserRequestedModule(GoalStep step)
		{
			var modules = typeHelper.GetRuntimeModules();
			List<string> forceModuleType = new List<string>();
			var match = Regex.Match(step.Text, @"\[[\w]+\]", RegexOptions.IgnoreCase | RegexOptions.Multiline);
			if (match.Success)
			{
				var matchValue = match.Value.ToLower().Replace("[", "").Replace("]", "");
				List<string> userRequestedModules = new List<string>();
				var module = modules.FirstOrDefault(p => p.Name.ToLower() == matchValue);
				if (module != null)
				{
					userRequestedModules.Add(module.Name);
				}
				else
				{
					foreach (var tmp in modules)
					{
						if (tmp.FullName != null && tmp.FullName.ToLower().Contains(matchValue.ToLower()))
						{
							userRequestedModules.Add(tmp.FullName.Replace(".Program", ""));
						}
					}
				}
				if (userRequestedModules.Count == 1)
				{
					forceModuleType.Add(userRequestedModules[0]);
				}
				else if (userRequestedModules.Count > 1)
				{
					forceModuleType = userRequestedModules;
				}
			}
			return forceModuleType;
		}
	}


}
