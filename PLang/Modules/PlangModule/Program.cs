using Force.DeepCloner;
using LightInject;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema.Generation;
using PLang.Attributes;
using PLang.Building;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Modules.PlangModule.Data;
using PLang.Runtime;
using PLang.SafeFileSystem;
using PLang.Utils;
using PLang.Variables;
using PLang.Variables.Errors;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static PLang.Modules.BaseBuilder;

namespace PLang.Modules.PlangModule
{
	[Description(@"get apps available, compiles plang code, gets goals and steps from .goal files, method descriptions and class scheme. Runtime Engine for plang, can run goal and step(s) in plang")]
	public class Program : BaseProgram
	{
		private readonly ILogger logger;
		private readonly IGoalParser goalParser;
		private readonly IPLangFileSystem fileSystem;
		private readonly IEngine engine;
		private readonly IGoalBuilder goalBuilder;
		private readonly PrParser prParser;
		private readonly IFileAccessHandler fileAccessHandler;

		public Program(ILogger logger, IGoalParser goalParser, IPLangFileSystem fileSystem, IEngine engine, IGoalBuilder goalBuilder, PrParser prParser, IFileAccessHandler fileAccessHandler) : base()
		{
			this.logger = logger;
			this.goalParser = goalParser;
			this.fileSystem = fileSystem;
			this.engine = engine;
			this.goalBuilder = goalBuilder;
			this.prParser = prParser;
			this.fileAccessHandler = fileAccessHandler;
		}

		[Description("parser=pr|goal")]
		public async Task<(PrApp? App, IError? Error)> GetApp(string path, string startingGoalName = "start", string parser = "pr")
		{
			var (app, error) = await GetPrApp(path, parser);
			
			if (error != null) return (null, error);
			
			if (string.IsNullOrEmpty(startingGoalName) || startingGoalName == "start")
			{
				app!.GoalToCall = new Models.GoalToCallInfo("Start")
				{
					Path = "/.build/start.pr"
				};
			}
			else
			{
				if (startingGoalName.EndsWith(".goal")) startingGoalName = startingGoalName.Replace(".goal", "");

				app!.GoalToCall = new Models.GoalToCallInfo(startingGoalName)
				{
					Path = fileSystem.Path.Join("/.build", $"{startingGoalName}.pr")
				};

			}

			return (app, error);
		}

		private async Task<(PrApp? app, IError? error)> GetPrApp(string path, string parser)
		{
			PrApp app;
			if (parser == "pr")
			{
				return prParser.GetPrApp();
			} else
			{
				(app, var error) = goalParser.GetPrApp();
				if (error != null) return (app, error);
			}
			return (app, null);
		}

		private async Task<(App app, IError error)> GetPrApp(string path)
		{
			throw new NotImplementedException();
		}
		private async Task<(List<PrGoal>? Goals, IError? Error)> GetPrGoals(string fileOrFolderPath, string visibility = "public", string parser = "pr")
		{
			var (goals, error) = await GetGoalsAsGoals(fileOrFolderPath, visibility, parser);
			if (error != null) return (null, error);

			List<PrGoal> prGoals = new();
			foreach (var goal in goals)
			{
				var steps = GetPrSteps(goal);

				PrGoal prGoal = new PrGoal(goal.GoalName, steps);
				prGoal.AbsolutePath = $"{fileSystem.RootDirectory}/{goal.RelativeGoalPath}/{goal.GoalFileName}".Replace("//", "/");

				prGoal.PrPath = $"{goal.RelativeGoalFolderPath}/.build/{goal.GoalName}";
				prGoal.DeveloperComment = goal.Comment;
				prGoal.FolderPath = goal.RelativePrFolderPath;
				prGoal.IsSetup = goal.IsSetup;
				prGoal.Path = goal.RelativeGoalPath;
				prGoals.Add(prGoal);
			}

			return (prGoals, error);
		}

		private List<PrStep> GetPrSteps(Goal goal)
		{
			List<PrStep> prSteps = new();
			foreach (var step in goal.GoalSteps)
			{
				var eventBindings = step.EventBinding;

				var prStep = new PrStep(step.Text, null, step.Index, null, step.ModuleType, null, step.Indent, step.Comment, null, null, null, null);
				prSteps.Add(prStep);
			}
			return prSteps;
		}

		private async Task<(List<Goal>? Goals, IError? Error)>  GetGoalsAsGoals(string fileOrFolderPath, string visibility = "public", string parser = "pr")
		{
			List<Goal> goals = new List<Goal>();
			string path = GetPath(fileOrFolderPath);

			if (parser == null)
			{
				parser = path.StartsWith(fileSystem.RootDirectory) ? "goal" : "pr";
			}

			if (parser == "pr")
			{
				if (path.EndsWith(".goal"))
				{
					goals = prParser.GetAllGoals().Where(p => p.AbsoluteGoalPath.Equals(path, StringComparison.OrdinalIgnoreCase)).ToList();
				}
				else if (path.EndsWith(".pr"))
				{
					goals = prParser.GetAllGoals().Where(p => p.AbsolutePrFilePath.Equals(path, StringComparison.OrdinalIgnoreCase)).ToList();
				}
				else if (fileSystem.Directory.Exists(path))
				{
					if (path.Contains(".build"))
					{
						goals = prParser.GetAllGoals().Where(p => p.AbsolutePrFolderPath.StartsWith(path, StringComparison.OrdinalIgnoreCase)).ToList();
					}
					else
					{
						goals = prParser.GetAllGoals().Where(p => p.AbsoluteGoalFolderPath.StartsWith(path, StringComparison.OrdinalIgnoreCase)).ToList();
					}
				}
				else
				{
					return (null, new ProgramError($"The path {fileOrFolderPath} could not be found, searched for it at {path}"));
				}

			}
			else if (parser == "goal")
			{
				var fileProgram = GetProgramModule<FileModule.Program>();
				var files = await fileProgram.GetFilePathsInDirectory(path, includeSubfolders: true, searchPattern: "*.goal");
				foreach (var file in files)
				{
					goals.AddRange(goalParser.ParseGoalFile(file.AbsolutePath));
				}
			}

			if (visibility == "public")
			{
				goals = goals.Where(p => p.Visibility == Visibility.Public).ToList();
			}
			if (visibility == "private")
			{
				goals = goals.Where(p => p.Visibility == Visibility.Private).ToList();
			}
			return (goals, null);
		}

		[Description("Get goals in file or folder. visiblity is either public|public_and_private|private. parser=pr|goal")]
		public async Task<(object? Goals, IError? Error)> GetGoals(string fileOrFolderPath, string visibility = "public", List<string>? propertiesToExtract = null, string parser = "pr")
		{
			var (goals, error) = await GetGoalsAsGoals(fileOrFolderPath, visibility, parser);
			if (error != null) return (null, error);
			/*
			 * should we return error if it's 0 goals?
			if (goals.Count == 0)
			{
				return (null, new ProgramError($"No goals found at {fileOrFolderPath}, the absolute path is: {path}"));
			}*/

			if (propertiesToExtract == null || propertiesToExtract.Count == 0) return (goals, null);

			JArray array = new JArray();
			foreach (var goal in goals)
			{
				var jObject = new JObject();
				foreach (var propertyName in propertiesToExtract)
				{

					var property = goal.GetType().GetProperties().FirstOrDefault(p => p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
					if (property != null)
					{
						var value = property.GetValue(goal);
						if (value != null)
						{
							jObject.Add(propertyName, value.ToString());
						}
					}

				}
				array.Add(jObject);
			}

			return (array, null);
		}


		[Description("Get all setup goals. visibility=public|private|public_and_private. propertiesToExtract define what properties from the goal should be extracted")]
		public async Task<(List<Goal>?, IError?)> GetSetupGoals(string? appPath = null, string visibility = "public", List<string>? propertiesToExtract = null)
		{
			var path = GetPath(appPath);

			var result = await GetGoals(appPath, visibility, propertiesToExtract);
			if (result.Error != null) return (null, result.Error);

			var goals = (List<Goal>)result.Goals;
			return (goals.Where(p => p.IsSetup).ToList(), null);
		}

		public async Task<(List<ClassDescription>?, IError?)> GetModules2()
		{
			var runtimeModules = typeHelper.GetRuntimeModules();
			List<ClassDescription> modules = new();
			foreach (var module in runtimeModules)
			{
				var classDescriptionHelper = new ClassDescriptionHelper();
				var (classDescription, error) = classDescriptionHelper.GetClassDescription(module);
				if (error != null) return (null, error);

				modules.Add(classDescription);
			}

			return (modules, null);

		}

		public async Task<(object?, IError?)> SaveGoal(Goal goal)
		{
			int i = 0;
			return (null, null);
		}
		public async Task<(object?, IError?)> SaveInstruction(Building.Model.Instruction instruction)
		{
			int i = 0;
			return (null, null);
		}

		public async Task<(ClassDescription?, IError?)> GetClassDescription(string module)
		{
			if (string.IsNullOrWhiteSpace(module)) return (null, null);

			var type = Type.GetType(module, false);
			if (type == null)
			{
				return (null, new ProgramError($"Could not load module '{module}'", goalStep));
			}

			var classDescriptionHelper = new ClassDescriptionHelper();
			return classDescriptionHelper.GetClassDescription(type);
		}

		protected T GetImplementation<T>(string name, Type defaultType)
		{
			if (context.Items.TryGetValue(GetType().FullName + "." + name, out object? value) && value != null)
			{
				return (T)value;
			}
			return SetImplementation<T>(name, defaultType);
		}
		protected T SetImplementation<T>(string name, Type type)
		{
			var instance = Activator.CreateInstance(type);
			context.Items.AddOrReplace(GetType().FullName + "." + name, instance);
			return (T)instance;
		}

		public async Task<(List<ObjectValue>?, IError?)> ValidateGoal(Goal goal)
		{
			/*
			var (steps, error) = ConvertToGoalSteps(goal, prGoal.GoalSteps);
			if (error != null) return (null, error);
			goal.Steps = steps;

			prGoal.Comment = goal.Comment;

			prGoal.Path = goal.RelativeGoalPath;
			prGoal.FolderPath = goal.RelativeGoalFolderPath;
			prGoal.AbsolutePath = goal.AbsoluteGoalPath;

			prGoal.PrFolderPath = fileSystem.Path.Join(prGoal.FolderPath, ".build");
			prGoal.PrPath = fileSystem.Path.Join(prGoal.PrFolderPath, prGoal.PrFileName);

			prGoal.IsEvent = goal.IsEvent;
			prGoal.IsSetup = goal.IsSetup;

			var list = new List<ObjectValue>();


			list.Add(new ObjectValue("goal", goal));
			list.Add(new ObjectValue("prGoal", prGoal));
			*/
			return (new(), null);
		}

		private (List<GoalStep>?, IError) ConvertToGoalSteps(PrGoal goal, IReadOnlyList<Data.PrStep> prSteps)
		{
			/*
			foreach (var prStep in prSteps)
			{
				var step = goal.Steps.FirstOrDefault(p => p.Index == prStep.Index);
				if (step == null) return (null, new Error($"Step index {prStep.Index} could not be found in {goal.Name} at {goal.Path}"));

				step.Module = prStep.Module;
				step.Indents = prStep.Indents;
				step. = prStep.Reasoning;
			}*/
			/*
			foreach (var prStep in goalSteps)
			{
				goalSteps.Add(GetAsGoalStep(prStep));
			}

			goal.GoalSteps = goalSteps;
			*/
			return new();
		}
		public async Task<IError?> ValidateFunction(string module, LlmStep llmStep)
		{
			MethodValidator mv = new();
			var error = mv.Validate(module, llmStep.Function.Name, llmStep.Function.Parameters);
			return error;

		}

		public async Task<List<VariableMatch>> ExtractVariables(PrStep step)
		{
			PlangVariableExtractor pve = new();
			return pve.ExtractVariables(JsonConvert.SerializeObject(step));
		}

		public async Task<(List<RuntimeVariable>?, IError?)> ValidateVariables(List<LlmVariable> llmVariables, PrStep step)
		{
			if (llmVariables == null || llmVariables.Count == 0)
			{
				return (null, null);
			}

			var helper = new VariableMappingHelper();
			var allRuntimeVariables = new List<RuntimeVariable>();

			// Convert step to JToken once for efficiency
			var stepJson = JsonConvert.SerializeObject(step);
			var stepToken = JToken.Parse(stepJson);

			foreach (var llmVariable in llmVariables)
			{
				var (runtimeVar, error) = helper.ValidateVariable(llmVariable);
				if (error != null)
				{
					return (null, error);
				}

				// For each property path, validate the variable exists there
				foreach (var propertyPath in llmVariable.PropertyPaths)
				{
					// Build JSONPath (add $ prefix if not present)
					var jsonPath = propertyPath.StartsWith("$") ? propertyPath : "$." + propertyPath;

					// Get the value at that path
					var token = stepToken.SelectToken(jsonPath);

					if (token == null)
					{
						var notFoundError = new VariableNotFoundError(
							$"Property path '{propertyPath}' not found in step");
						return (null, notFoundError);
					}

					var originalText = token.ToString();

					if (string.IsNullOrEmpty(originalText))
					{
						var notFoundError = new VariableNotFoundError(
							$"Property path '{propertyPath}' is empty in step");
						return (null, notFoundError);
					}

					// Verify the variable expression exists in that text
					int start = originalText.IndexOf(llmVariable.FullExpression);
					if (start == -1)
					{
						var notFoundError = new VariableNotFoundError(
							$"Variable '{llmVariable.FullExpression}' not found in {propertyPath}. Found: '{originalText}'");
						
						return (null, notFoundError);
					}
				}

				allRuntimeVariables.Add(runtimeVar);
			}


			return (allRuntimeVariables, null);
		}


		public async Task<List<ClassDescription>> GetPipedClasses()
		{
			PipedClassesHelper helper = new PipedClassesHelper();
			var types = helper.GetPipedClasses();
			if (types == null) return new();

			List<ClassDescription> classDescriptions = new List<ClassDescription>();
			foreach (var type in types)
			{
				ClassDescriptionHelper ch = new ClassDescriptionHelper();
				ch.GetClassDescription(type);
			}
			return classDescriptions;
		}

		public async Task<(PrStep?, IError?)> ValidateStep(PrStep step, LlmStep llmStep)
		{
			step = step with
			{
				Reasoning = llmStep.Reasoning,
				Function = llmStep.Function,
				CacheHandler = llmStep.CacheHandler,
				ErrorHandlers = llmStep.ErrorHandlers,
				BeforeEventHandlers = llmStep.BeforeEventHandlers,
				AfterEventHandlers = llmStep.AfterEventHandlers,
				PrRunAndForget = llmStep.PrRunAndForget, 
				LlmComments = llmStep.LlmComments,
			};

			MethodValidator mv = new();
			var error = mv.Validate(step.Module, llmStep.Function.Name, llmStep.Function.Parameters);
			if (error != null) return (step, error);

			int i = 0;
			return (step, null);
		}
		public async Task<(string?, IError?)> GetModules(string? stepText = null, List<string>? excludeModules = null)
		{
			if (string.IsNullOrEmpty(stepText))
			{
				stepText = goalStep.Text;
			}
			var modulesAvailable = typeHelper.GetModulesAsString(excludeModules);
			var userRequestedModule = GetUserRequestedModule(stepText);
			if (excludeModules != null && excludeModules.Count == 1 && userRequestedModule.Count == 1
				&& userRequestedModule.FirstOrDefault(p => p.Equals(excludeModules[0])) != null)
			{
				return (null, new BuilderError($"Could not map {stepText} to {userRequestedModule[0]}"));
			}

			if (userRequestedModule.Count > 0)
			{
				modulesAvailable = string.Join(", ", userRequestedModule);
			}
			return (modulesAvailable, null);
		}

		[Description("Returns the class description with methods in a module")]
		public async Task<(ClassDescription?, IError?)> GetClassDescription(string moduleName, List<string>? methodNames = null)
		{
			var programType = typeHelper.GetRuntimeType(moduleName);

			var classDescriptionHelper = new ClassDescriptionHelper();
			return classDescriptionHelper.GetClassDescription(programType, methodNames);

		}

		public async Task<(string, IError?)> GetSCharpCode(string solutionPath, string type)
		{


			return (null, null);
		}

		public async Task<(object, IError)> Run(string @namespace, string @class, string method, Dictionary<string, object?>? Parameters)
		{
			return (null, null);
			//return genericFunction.Run(@namespace, @class, method, Parameters);
		}

		public async Task<string> GetMethodMappingScheme()
		{
			string scheme = TypeHelper.GetJsonSchema(typeof(MethodExecution));
			return scheme;
		}
		/*
		 * TODO: Dictionary<string, object?> Parameters should be Parameters class 
		 * */
		public async Task<(object obj, IError?)> RunModule(string @namespace, string @class, string method, Dictionary<string, object?>? Parameters)
		{
			var programType = typeHelper.Run(@namespace, @class, method, Parameters);
			return (null, new Error(ErrorReporting.CreateIssueNotImplemented));
		}

		public async Task<(Dictionary<string, object>?, IError?)> GetStepProperties(string moduleName, string methodName)
		{
			bool canBeCached = true;
			bool canHaveErrorHandling = true;
			bool canBeCancelled = true;
			bool canBeAsync = true;

			Dictionary<string, object> properties = new();

			var moduleType = typeHelper.GetRuntimeType(moduleName);
			if (moduleType == null)
			{
				return (null, new BuilderError($"Could not find {moduleName} in list of available modules."));
			}
			if (moduleType != null)
			{
				var method = moduleType.GetMethods(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(p => p.Name == methodName);
				if (method == null)
				{
					return (null, new BuilderError($"Could not find {methodName} in {moduleName} in list of available methods."));
				}

				var attribute = method.GetCustomAttribute<MethodSettingsAttribute>();

				if (attribute != null)
				{
					canBeCached = attribute.CanBeCached;
					canHaveErrorHandling = attribute.CanHaveErrorHandling;
					canBeAsync = attribute.CanBeAsync;
					canBeCancelled = attribute.CanBeCancelled;
				}
			}

			if (canBeAsync)
			{
				properties.Add("WaitForExecution", "{WaitForExecution:bool = true}");
			}
			if (canBeCached)
			{
				properties.Add("CachingHandler", TypeHelper.GetJsonSchema(typeof(CachingHandler)));
			}
			if (canHaveErrorHandling)
			{
				properties.Add("ErrorHandler", TypeHelper.GetJsonSchema(typeof(ErrorHandler)));
			}

			if (canBeCancelled)
			{
				properties.Add("CancellationHandler", TypeHelper.GetJsonSchema(typeof(CancellationHandler)));
			}

			return (properties, null);
		}

		private List<string> GetUserRequestedModule(string stepText)
		{
			var modules = typeHelper.GetRuntimeModules();
			List<string> forceModuleType = new List<string>();
			var match = Regex.Match(stepText, @"\[[\w]+\]", RegexOptions.IgnoreCase | RegexOptions.Multiline);
			if (match.Success)
			{
				var matchValue = match.Value.ToLower().Replace("[", "").Replace("]", "");
				List<string> userRequestedModules = new List<string>();
				var module = modules.FirstOrDefault(p => p.FullName.Equals(matchValue, StringComparison.OrdinalIgnoreCase));
				if (module != null)
				{
					userRequestedModules.Add(module.Name);
				}
				else
				{
					foreach (var tmp in modules)
					{
						if (tmp.FullName != null && tmp.FullName.Replace("PLang.Modules.", "").Contains(matchValue, StringComparison.OrdinalIgnoreCase))
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

		public async Task<List<ObjectValue>> GetVariables(GoalStep step)
		{
			if (step == null) return new();

			var variables = variableHelper.GetVariables(step.Text, memoryStack);
			return variables;
		}

		public async Task<(object?, IError?)> RunFunction(IGenericFunction function)
		{
			return await base.RunFunction(function);
		}

		public async Task<(IReadOnlyList<GoalStep>?, IError?)> GetSteps(string goalPath)
		{

			string absoluteGoalPath = GetPath(goalPath);
			string ext = fileSystem.Path.GetExtension(absoluteGoalPath);
			if (string.IsNullOrEmpty(ext))
			{
				absoluteGoalPath += ".goal";
			}

			if (absoluteGoalPath.EndsWith(".goal"))
			{
				var goals = goalParser.ParseGoalFile(absoluteGoalPath);
				if (goals.Count > 0)
				{
					absoluteGoalPath = goals[0].AbsolutePrFilePath;
				}
			}

			var goal = prParser.ParsePrFile(absoluteGoalPath);
			if (goal == null)
			{
				return (null, new ProgramError($"Goal at {goalPath} could not be found", goalStep, function));
			}
			return (goal.GoalSteps, null);
		}

		public async Task<(GoalStep?, IError?)> GetStep(string goalPrPath, string stepPrFile)
		{
			string absoluteGoalPath = GetPath(goalPrPath);
			var goal = prParser.ParsePrFile(absoluteGoalPath);

			if (goal == null)
			{
				return (null, new ProgramError($"Goal at {goalPrPath} could not be found", goalStep, function));
			}

			var step = goal.GoalSteps.FirstOrDefault(p => p.PrFileName == stepPrFile);
			if (step == null)
			{
				return (null, new ProgramError($"Step {stepPrFile} in {goalPrPath} could not be found", goalStep, function));
			}
			if (fileSystem.File.Exists(step.AbsolutePrFilePath))
			{
				string content = fileSystem.File.ReadAllText(step.AbsolutePrFilePath);
				step.PrFile = JsonConvert.DeserializeObject(content);
				if (step.ModuleType == "PLang.Modules.ConditionalModule" || step.ModuleType == "PLang.Modules.CodeModule")
				{
					var jobject = step.PrFile as JObject;
					string? code = jobject?["Action"]?["Parameters"]?[0]?["Value"]?["Code"]?.ToString();
					if (code != null)
					{
						jobject["Action"]["Parameters"][0]["Value"]["Code"] = code.Replace("α", ".");
						step.PrFile = jobject;
					}
				}
			}

			return (step, null);
		}

		[Description("Runs a plang step. No other step is executed")]
		public async Task<(object?, IError?)> RunStep(GoalStep step, Dictionary<string, object?>? parameters = null)
		{
			var startingEngine = engine.GetAppContext()[ReservedKeywords.StartingEngine] as IEngine;
			if (startingEngine == null) startingEngine = engine;

			//engine.GetContext().Remove(ReservedKeywords.IsEvent);

			fileAccessHandler.GiveAccess(fileSystem.SystemDirectory, fileSystem.GoalsPath);
			if (parameters != null)
			{
				var ms = context.MemoryStack;
				foreach (var parameter in parameters)
				{
					ms.Put(parameter.Key, parameter.Value);
				}
			}
			var result = await startingEngine.ProcessPrFile(step.Goal, step, step.Number, context);
			return result;
		}


		[Description("Run from a specific step and the following steps")]
		public async Task<(object?, IError?)> RunFromStep(string prFileName)
		{

			if (string.IsNullOrEmpty(prFileName))
			{
				return (null, new ProgramError($"prFileName is empty. I cannot run a step if I don't know what to run.", goalStep, function,
					FixSuggestion: "Something has broken between the IDE sending the information and the runtime. Check if SendDebug.goal and the IDE is talking together correctly."));
			}
			var absoluteFilePath = GetPath(prFileName);
			fileAccessHandler.GiveAccess(fileSystem.SystemDirectory, fileSystem.GoalsPath);
			if (!fileSystem.File.Exists(absoluteFilePath))
			{
				return (null, new ProgramError($"The file {prFileName} could not be found. I searched for it at {absoluteFilePath}", goalStep, function));
			}
			// todo: attaching debugger and running from step does not work for http requests
			// Run from step should also be a callback, like is done on websites (stateless)
			// this validates that the user sending calls RunFromStep is valid
			var startingEngine = engine.GetAppContext()[ReservedKeywords.StartingEngine] as IEngine;
			if (startingEngine == null) startingEngine = engine;

			engine.GetEventRuntime().SetActiveEvents(new());

			var result = await startingEngine.RunFromStep(absoluteFilePath, context);
			return result;
		}

		[Description("Builds(compiles) a step in plang code")]
		public async Task<(GoalStep?, IError?)> BuildPlangStep(GoalStep? step)
		{
			if (step == null)
			{
				return (null, new ProgramError("Step is not provided. I cannot continue to build a step that is not provided", goalStep, function));
			}
			if (fileSystem.File.Exists(step.AbsolutePrFilePath))
			{
				fileSystem.File.Delete(step.AbsolutePrFilePath);
			}

			var builder = Container.GetInstance<IBuilder>();
			var error = await builder.Start(Container, context, step.Goal.AbsoluteGoalPath);

			var goals = prParser.ForceLoadAllGoals();
			var goal = goals.FirstOrDefault(p => p.AbsolutePrFilePath == step.Goal.AbsolutePrFilePath);
			if (goal == null) return (null, new StepError($"Could not find goal after building. {ErrorReporting.CreateIssueShouldNotHappen}", step));

			GoalStep? newStep = newStep = goal.GoalSteps.FirstOrDefault(p => p.Number == step.Number);
			if (newStep == null) return (null, new StepError($"Could not find step after building. {ErrorReporting.CreateIssueShouldNotHappen}", step));

			if (!fileSystem.File.Exists(step.AbsolutePrFilePath))
			{
				return (newStep, new StepError($"Could not find instruction file after building. {ErrorReporting.CreateIssueShouldNotHappen}", step, StatusCode: 404));
			}

			newStep.PrFile = fileSystem.File.ReadAllText(step.AbsolutePrFilePath);

			return (newStep, error);
		}

		[Description("Builds(compiles) a goal in plang code")]
		public async Task<IError?> BuildPlangCode(Goal goal)
		{
			var builder = Container.GetInstance<IBuilder>();
			var error = await builder.Start(Container, context, goal.AbsoluteGoalPath);

			prParser.ForceLoadAllGoals();
			return error;
		}

		public async Task StartCSharpDebugger()
		{
			if (Debugger.IsAttached) return;

			Debugger.Launch();
			AppContext.SetSwitch(ReservedKeywords.CSharpDebug, true);
			AppContext.SetSwitch(ReservedKeywords.DetailedError, true);

		}

		public async Task<(ClassDescription?, IError?)> GetMehodInfo(string type, string methodName)
		{
			var cdh = new ClassDescriptionHelper();
			return cdh.GetClassDescription(Type.GetType(type + ".Program"), [methodName]);

		}



		public class MethodInfoDto
		{
			public string Name { get; set; }
			public string Description { get; set; }
			public string ReturnType { get; set; }
			public List<(string Type, string Name)> Parameters { get; set; }
			public string Code { get; set; }
		}

		public static List<MethodInfoDto> GetCSharpCode(string filePath)
		{
			var code = File.ReadAllText(filePath);
			var tree = CSharpSyntaxTree.ParseText(code);
			var root = tree.GetRoot();
			var methods = new List<MethodInfoDto>();

			var methodNodes = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
			foreach (var method in methodNodes)
			{
				var description = method.AttributeLists
					.SelectMany(a => a.Attributes)
					.FirstOrDefault(a => a.Name.ToString().Contains("Description"));

				var descValue = description?.ArgumentList?.Arguments.FirstOrDefault()?.ToString().Trim('"') ?? string.Empty;

				var parameters = method.ParameterList.Parameters
					.Select(p => (p.Type?.ToString() ?? "var", p.Identifier.Text))
					.ToList();

				var returnType = method.ReturnType.ToString();

				methods.Add(new MethodInfoDto
				{
					Name = method.Identifier.Text,
					Description = descValue,
					ReturnType = NormalizeReturnType(method.ReturnType),
					Parameters = parameters,
					Code = method.ToFullString()
				});
			}
			return methods;
		}

		private static string NormalizeReturnType(TypeSyntax returnTypeSyntax)
		{
			var returnType = returnTypeSyntax.ToString();

			if (returnType.StartsWith("Task<"))
			{
				var taskInnerType = returnType.Substring(5, returnType.Length - 6).Trim();
				return $"Task<{SimplifyTuple(taskInnerType)}>";
			}
			else if (returnType == "Task")
			{
				return "Task";
			}

			return returnType;
		}

		private static string SimplifyTuple(string type)
		{
			if (type.StartsWith("(") && type.EndsWith(")"))
			{
				return type;
			}
			return type;
		}
	}
}

