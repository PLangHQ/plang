﻿using Microsoft.Extensions.Logging;
using NBitcoin.Secp256k1;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.AskUser;
using PLang.Errors.Builder;
using PLang.Errors.Handlers;
using PLang.Events;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Models;
using PLang.Modules.DbModule;
using PLang.Runtime;
using PLang.Services.CompilerService;
using PLang.Services.LlmService;
using PLang.Utils;
using RazorEngineCore;
using System.ComponentModel;
using System.Text.RegularExpressions;
using static PLang.Modules.BaseBuilder;
using static PLang.Modules.DbModule.ModuleSettings;

namespace PLang.Building
{
	public interface IStepBuilder
	{
		Task<IBuilderError?> BuildStep(Goal goal, int stepNr, List<string>? excludeModules = null, int errorCount = 0);
	}

	public class StepBuilder : IStepBuilder
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly ILlmServiceFactory llmServiceFactory;
		private readonly Lazy<ILogger> logger;
		private readonly IInstructionBuilder instructionBuilder;
		private readonly IEventRuntime eventRuntime;
		private readonly ITypeHelper typeHelper;
		private readonly MemoryStack memoryStack;
		private readonly VariableHelper variableHelper;
		private readonly IErrorHandlerFactory exceptionHandlerFactory;
		private readonly PLangAppContext context;
		private readonly ISettings settings;

		public StepBuilder(Lazy<ILogger> logger, IPLangFileSystem fileSystem, ILlmServiceFactory llmServiceFactory,
					IInstructionBuilder instructionBuilder, IEventRuntime eventRuntime, ITypeHelper typeHelper,
					MemoryStack memoryStack, VariableHelper variableHelper, IErrorHandlerFactory exceptionHandlerFactory, 
					PLangAppContext context, ISettings settings)
		{
			this.fileSystem = fileSystem;
			this.llmServiceFactory = llmServiceFactory;
			this.logger = logger;
			this.instructionBuilder = instructionBuilder;
			this.eventRuntime = eventRuntime;
			this.typeHelper = typeHelper;
			this.memoryStack = memoryStack;
			this.variableHelper = variableHelper;
			this.exceptionHandlerFactory = exceptionHandlerFactory;
			this.context = context;
			this.settings = settings;
		}

		public async Task<IBuilderError?> BuildStep(Goal goal, int stepIndex, List<string>? excludeModules = null, int errorCount = 0)
		{
			var step = (stepIndex < goal.GoalSteps.Count) ? goal.GoalSteps[stepIndex] : null;
			if (step == null)
			{
				return new GoalBuilderError($"Step nr. {stepIndex + 1} could not be loaded from goal {goal.GoalName}. This is unusual behaviour and should not happen. Try deleting the .pr file from {goal.AbsolutePrFolderPath}.", goal);
			}
			if (++errorCount > 3)
			{
				return new StepBuilderError($"Could not get answer from LLM. Will NOT try again. Tried {errorCount} times. Will continue to build next step.", step);
			}

			if (excludeModules == null) { excludeModules = new List<string>(); }

			try
			{
				var strStepNr = (stepIndex + 1).ToString().PadLeft(2, '0');
				if (StepHasBeenBuild(step, stepIndex, excludeModules)) return null;

				await eventRuntime.RunBuildStepEvents(EventType.Before, goal, step, stepIndex);

				LlmRequest llmQuestion = GetBuildStepQuestion(goal, step, excludeModules);

				logger.Value.LogInformation($"- Find module for {step.Text}");
				llmQuestion.Reload = false;
				(var stepAnswer, var llmError) = await llmServiceFactory.CreateHandler().Query<StepAnswer>(llmQuestion);
				if (llmError != null) return llmError as IBuilderError;

				if (stepAnswer == null)
				{
					logger.Value.LogWarning($"Could not get answer from LLM. Will try again. This is attempt nr {++errorCount}");
					return await BuildStep(goal, stepIndex, excludeModules, errorCount);
				}

				var module = stepAnswer.Modules.FirstOrDefault();
				var moduleType = typeHelper.GetRuntimeType(module);
				if (moduleType == null || module == null || module == "N/A")
				{
					return new StepBuilderError($@"Could not find module for {step.Text}. 
Try defining the step in more detail.

You have 3 options:
	- Rewrite your step to fit better with a modules that you have installed. 
		How to write the step? Get help here https://github.com/PLangHQ/plang/blob/main/Documentation/modules/README.md
	- Install an App from that can handle your request and call that
	- Build your own module. This requires a C# developer knowledge

Builder will continue on other steps but not this one: ({step.Text}).
", step);

				}

				step.ModuleType = module;
				step.ErrorHandler = stepAnswer.ErrorHandler;
				step.WaitForExecution = stepAnswer.WaitForExecution;
				step.RetryHandler = stepAnswer.RetryHandler;
				// cannot put caching on caching
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

				var error = await instructionBuilder.BuildInstruction(this, goal, step, module, stepIndex, excludeModules, errorCount);
				if (error != null)
				{
					if (error is not InvalidFunctionsError)
					{
						return error;
					}

					if (error is InvalidFunctionsError invalidFunctions)
					{
						if (invalidFunctions.ExcludeModule && !excludeModules.Contains(module))
						{
							excludeModules.Add(module);
						}

					}
					logger.Value.LogWarning(error.Message);
					var buildStepError = await BuildStep(goal, stepIndex, excludeModules, ++errorCount);
					if (buildStepError != null)
					{
						return buildStepError;
					}
				}
				//Set reload after Build Instruction
				step.Reload = false;
				step.Generated = DateTime.Now;

				return await eventRuntime.RunBuildStepEvents(EventType.After, goal, step, stepIndex);
			}
			catch (Exception ex)
			{
				IBuilderError error;
				if (ex.InnerException is PLang.Errors.Handlers.AskUserError)
				{
					ex = ex.InnerException;
				}

				if (ex is PLang.Errors.Handlers.AskUserError mse)
				{
					Console.WriteLine(mse.Message);
					var line = Console.ReadLine();

					await mse.InvokeCallback(line);
					return await BuildStep(goal, stepIndex, excludeModules, errorCount);
				}
				else
				{
					error = new ExceptionError(ex, Step: step, Goal: goal);
				}
				(var isHandled, var handlerError) = await exceptionHandlerFactory.CreateHandler().Handle(error);
				if (isHandled)
				{
					return await BuildStep(goal, stepIndex, excludeModules, errorCount);
				}
				else
				{
					if (handlerError == null) return error;

					return ErrorHelper.GetMultipleBuildError(error, handlerError);
				}
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
				LoadVariablesIntoMemoryStack(gf, memoryStack, context, settings);
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

			logger.Value.LogInformation($"- Step {step.Name} is already built");
			return true;
		}

		public void LoadVariablesIntoMemoryStack(GenericFunction? gf, MemoryStack memoryStack, PLangAppContext context, ISettings settings)
		{
			if (gf == null) return;

			if (gf.ReturnValue != null && gf.ReturnValue.Count > 0)
			{
				foreach (var returnValue in gf.ReturnValue)
				{
					memoryStack.PutForBuilder(returnValue.VariableName, returnValue.Type);
				}
			}

			LoadParameters(gf, memoryStack, context, settings);
		}

		private void LoadParameters(GenericFunction? gf, MemoryStack memoryStack, PLangAppContext context, ISettings settings)
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


			// this is bad implementation, the Builder.cs of the module should 
			// handle any custom loading into memoryStack
			if (gf.FunctionName == "RunGoal")
			{
				var json = gf.Parameters.FirstOrDefault(p => p.Name == "parameters")?.Value;
				if (json == null || !JsonHelper.IsJson(json)) return;
				var parameters = JsonConvert.DeserializeObject<Dictionary<string, object>>(json.ToString());
				if (parameters == null) return;

				foreach (var parameter in parameters)
				{
					memoryStack.PutForBuilder(parameter.Key, parameter.Value);
				}
			}
			

			// todo: also bad implementation, builder for module should handle this part
			if (gf.FunctionName == "CreateDataSource" || gf.FunctionName == "SetDataSourceName")
			{
				var parameter = gf.Parameters.FirstOrDefault(p => p.Name == "name");
				if (parameter == null) return;

				var dataSourceName = parameter.Value.ToString() ?? "data";
				var datasources = settings.GetValues<DataSource>(typeof(PLang.Modules.DbModule.ModuleSettings)).ToList();
				var datasource = datasources.FirstOrDefault(p => p.Name == dataSourceName);
				var isDefaultForApp = ((bool?)gf.Parameters.FirstOrDefault(p => p.Name == "setAsDefaultForApp")?.Value) ?? false;

				if (datasource == null)
				{					
					var keepHistoryEventSourcing = ((bool?)gf.Parameters.FirstOrDefault(p => p.Name == "keepHistoryEventSourcing")?.Value) ?? false;
					var databaseType = gf.Parameters.FirstOrDefault(p => p.Name == "databaseType")?.Value?.ToString() ?? "sqlite";
					var localPath = "";
					if (databaseType == "sqlite")
					{
						localPath = gf.Parameters.FirstOrDefault(p => p.Name == "localPath")?.Value?.ToString();
						if (string.IsNullOrEmpty(localPath)) localPath = "./.db/data.sqlite";
					}

					var moduleSettings = new ModuleSettings(fileSystem, settings, context, llmServiceFactory, logger.Value);
					moduleSettings.CreateDataSource(dataSourceName, localPath, databaseType, isDefaultForApp, keepHistoryEventSourcing).Wait();

					datasources = settings.GetValues<DataSource>(typeof(PLang.Modules.DbModule.ModuleSettings)).ToList();
					datasource = datasources.FirstOrDefault(p => p.Name == dataSourceName);
				}

				
				if ((gf.FunctionName == "CreateDataSource" && isDefaultForApp) || gf.FunctionName == "SetDataSourceName")
				{
					context.AddOrReplace(ReservedKeywords.CurrentDataSource, datasource);
				}
			}



		}

		private LlmRequest GetBuildStepQuestion(Goal goal, GoalStep step, List<string>? excludeModules = null)
		{
			// user might define in his step specific module.

			var modulesAvailable = typeHelper.GetModulesAsString(excludeModules);
			var userRequestedModule = GetUserRequestedModule(step);
			if (excludeModules != null && excludeModules.Count == 1 && userRequestedModule.Count == 1
				&& userRequestedModule.FirstOrDefault(p => p.Equals(excludeModules[0])) != null)
			{
				throw new BuilderStepException($"Could not map {step.Text} to {userRequestedModule[0]}");
			}

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
WaitForExecution: Default is true. Indicates if code should wait for execution to finish.
ErrorHandler: How to handle errors defined by user, default is null. if error should be handled but text (OnExceptionContainingTextCallGoal) is not defined, then use * for key
RetryHandler: How to retry the step if there is error, default is null
CachingHandler: How caching is handled, default is null
Read the description of each module, then determine which module to use.
Make sure to return valid JSON, escape double quote if needed

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
