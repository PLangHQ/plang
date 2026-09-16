using Force.DeepCloner;
using LightInject;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Attributes;
using PLang.Building;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.SafeFileSystem;
using PLang.Utils;
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

		[Description("Get goals in file or folder. visiblity is either public|public_and_private|private. parser=pr|goal")]
		public async Task<(object? Goals, IError? Error)> GetGoals(string fileOrFolderPath, string visibility = "public", List<string>? propertiesToExtract = null, string parser = "pr")
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
						goals = prParser.GetAllGoals().Where(p => p.AbsoluteGoalPath.StartsWith(path, StringComparison.OrdinalIgnoreCase)).ToList();
					}
				}
				else
				{
					return (null, new ProgramError($"The path {fileOrFolderPath} could not be found, searched for it at {path}"));
				}

			} else if (parser == "goal")
			{
				// The description says file or folder, but the folder walk below returns nothing for a
				// file path, so parsing one named .goal file gave an empty list. A caller that names a
				// single file is the normal case for a targeted build, so parse that file directly.
				if (path.EndsWith(".goal", StringComparison.OrdinalIgnoreCase))
				{
					if (!fileSystem.File.Exists(path))
					{
						return (null, new ProgramError($"The goal file {fileOrFolderPath} could not be found, searched for it at {path}"));
					}
					goals = goalParser.ParseGoalFile(path);
				}
				else
				{
					var fileProgram = GetProgramModule<FileModule.Program>();
					var files = await fileProgram.GetFilePathsInDirectory(fileOrFolderPath, includeSubfolders: true, searchPattern: "*.goal");
					foreach (var file in files)
					{
						goals.AddRange(goalParser.ParseGoalFile(file.AbsolutePath));
					}
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
			/*
			 * should we return error if it's 0 goals?
			if (goals.Count == 0)
			{
				return (null, new ProgramError($"No goals found at {fileOrFolderPath}, the absolute path is: {path}"));
			}*/
			goals.ForEach(p => p.CurrentPath = p.RelativeGoalPath.Replace(goal.RelativeGoalFolderPath, ""));
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

		public record Runtime(List<Module> Modules);
		public record Module(Type Type, ClassDescription ClassDescription);

		public async Task<(object?, IError?)> GetModules2(string? format = null)
		{
			var runtime = new Runtime(new());

			
			var modules = typeHelper.GetRuntimeModules();
			
			foreach (var module in modules)
			{
				var classDescriptionHelper = new ClassDescriptionHelper();
				var (classDescription, error) = classDescriptionHelper.GetClassDescription(module);
				if (error != null) return (null, error);

				runtime.Modules.Add(new Module(module, classDescription));
			}

			if (format?.Equals("md", StringComparison.OrdinalIgnoreCase) == true)
			{
				var template = GetProgramModule<TemplateEngineModule.Program>();

				var parameters = new Dictionary<string, object?>();
				parameters.Add("runtime", runtime);

				return await template.RenderFile("/system/modules/plang/templates/GetModules.html", parameters);
				
			}

			return (runtime, null);

		}

		public async Task<(object?, IError?)> SaveGoal(Goal goal)
		{
			int i = 0;
			return (null, null);
		}
		public async Task<(object?, IError?)> SaveMethod(object methodPr)
		{
			int i = 0;
			return (null, null);
		}
		public async Task<(object?, IError?)> GetMethods(List<string> modules, string? format = null)
		{
			int i = 0;
			return (null, null);
		}

		public async Task<(object?, IError?)> ValidateGoal(Goal goal)
		{
			int i = 0;
			return (null, null);
		}

		public async Task<(object?, IError?)> ValidateMethod(GoalStep step, object function)
		{
			int i = 0;
			return (null, null);
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
		public async Task<(ClassDescription?, IError?)> GetClassDescription(string moduleName)
		{
			var programType = typeHelper.GetRuntimeType(moduleName);

			var classDescriptionHelper = new ClassDescriptionHelper();
			return classDescriptionHelper.GetClassDescription(programType);

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
		public record ModuleInfo(string Name, string? Description);

		[Description("Lists the runtime modules by full name with the module description")]
		public async Task<(List<ModuleInfo>?, IError?)> ListModules()
		{
			var list = new List<ModuleInfo>();
			foreach (var module in typeHelper.GetRuntimeModules())
			{
				var classDescriptionHelper = new ClassDescriptionHelper();
				var (classDescription, error) = classDescriptionHelper.GetClassDescription(module);
				if (error != null) return (null, error);

				list.Add(new ModuleInfo(module.FullName!.Replace(".Program", ""), classDescription?.Description));
			}
			return (list, null);
		}

		[Description("Runs a method on a runtime module by name at runtime, e.g. moduleName=PLang.Modules.FileModule, method=ReadTextFile, parameters={path:\"file.txt\"}. Parameter names must match the method's parameter names. Relative paths resolve from the app root when fromAppRoot is true, otherwise from the calling goal's folder. Returns what the method returns")]
		public async Task<(object? Result, IError? Error)> RunModule(string moduleName, string method, Dictionary<string, object?>? parameters = null, bool fromAppRoot = false)
		{
			var programType = typeHelper.GetRuntimeType(moduleName);
			if (programType == null)
			{
				return (null, new ProgramError($"Module {moduleName} not found", goalStep, Key: "ModuleNotFound", StatusCode: 404));
			}

			var program = container.GetInstance(programType) as BaseProgram;
			if (program == null)
			{
				return (null, new ProgramError($"Could not create {moduleName}", goalStep, Key: "ModuleNotCreated", StatusCode: 500));
			}

			var goalToRunAs = goal;
			if (fromAppRoot)
			{
				goalToRunAs = goal.ShallowClone();
				goalToRunAs.AbsoluteGoalFolderPath = fileSystem.RootDirectory;
				goalToRunAs.RelativeGoalFolderPath = fileSystem.Path.DirectorySeparatorChar.ToString();
			}
			program.Init(container, goalToRunAs, goalStep, instruction, contextAccessor);
			if (program is IAsyncConstructor asyncConstructor)
			{
				var error = await asyncConstructor.AsyncConstructor();
				if (error != null) return (null, error);
			}

			var candidates = programType.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => m.Name == method).ToList();
			if (candidates.Count == 0)
			{
				return (null, new ProgramError($"Method {method} not found in {moduleName}", goalStep, Key: "MethodNotFound", StatusCode: 404));
			}
			var parameterNames = parameters?.Keys.ToList() ?? new List<string>();
			var chosen = candidates.FirstOrDefault(m => parameterNames.All(n => m.GetParameters().Any(p => p.Name == n))) ?? candidates[0];

			var functionParameters = new List<Parameter>();
			if (parameters != null)
			{
				foreach (var parameter in parameters)
				{
					var methodParameter = chosen.GetParameters().FirstOrDefault(p => p.Name == parameter.Key);
					var type = methodParameter?.ParameterType.FullNameNormalized() ?? parameter.Value?.GetType().FullName ?? "System.Object";
					functionParameters.Add(new Parameter(type, parameter.Key, parameter.Value));
				}
			}
			var genericFunction = new GenericFunction("", method, functionParameters, null);
			genericFunction.Instruction = instruction;

			return await program.RunFunction(genericFunction);
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

		public async Task<(object?, IError?)> RunFunction(ObjectValue genericFunction)
		{
			var value = genericFunction.Value as string;
			var genericeFunction = JsonConvert.DeserializeObject<IGenericFunction>(value);
			return await base.RunFunction(genericeFunction);
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
			var result = await engine.RunFromStep(absoluteFilePath, context);
			if (result.Error == null)
			{
				return (result.ReturnValue, new EndGoal(true, goal, goalStep, "Ending Run from step exeuction"));
			}
			return result;
		}

		[Description("Builds(compiles) a step in plang code")]
		public async Task<(GoalStep?, List<IBuilderError>?)> BuildPlangStep(GoalStep? step)
		{
			if (step == null)
			{
				return (null, [new BuilderError("Step is not provided. I cannot continue to build a step that is not provided") { Step = goalStep, Goal = Goal }]);
			}
			if (fileSystem.File.Exists(step.AbsolutePrFilePath))
			{
				fileSystem.File.Delete(step.AbsolutePrFilePath);
			}

			var builder = Container.GetInstance<IBuilder>();
			var error = await builder.Start(Container, BuildContext(), new[] { step.Goal.AbsoluteGoalPath });

			var goals = prParser.ForceLoadAllGoals();
			var goal = goals.FirstOrDefault(p => p.AbsolutePrFilePath == step.Goal.AbsolutePrFilePath);
			if (goal == null) return (null, [new StepBuilderError($"Could not find goal after building. {ErrorReporting.CreateIssueShouldNotHappen}", step)]);

			GoalStep? newStep = newStep = goal.GoalSteps.FirstOrDefault(p => p.Number == step.Number);
			if (newStep == null) return (null, [new StepBuilderError($"Could not find step after building. {ErrorReporting.CreateIssueShouldNotHappen}", step)]);

			if (!fileSystem.File.Exists(step.AbsolutePrFilePath))
			{
				return (newStep, [new StepBuilderError($"Could not find instruction file after building. {ErrorReporting.CreateIssueShouldNotHappen}", step, StatusCode: 404)]);
			}

			newStep.PrFile = fileSystem.File.ReadAllText(step.AbsolutePrFilePath);

			return (newStep, error);
		}

		[Description("Builds(compiles) one or more goals in plang code. Pass a single goal or a list of goals, e.g. from 'get goals in \"/folder\"'. Every distinct .goal file among them is compiled. Returns the builder errors, or null when all built.")]
		public async Task<List<IBuilderError>?> BuildPlangCode(List<Goal> goals)
		{
			if (goals == null || goals.Count == 0) return null;

			// One .goal file can hold several goals, so build by distinct file, not per goal.
			var paths = goals.Where(g => g?.AbsoluteGoalPath != null)
							 .Select(g => g.AbsoluteGoalPath)
							 .Distinct(StringComparer.OrdinalIgnoreCase)
							 .ToList();
			if (paths.Count == 0) return null;

			var builder = Container.GetInstance<IBuilder>();
			var error = await builder.Start(Container, BuildContext(), paths);

			prParser.ForceLoadAllGoals();
			return error;
		}

		// The builder mutates the context it is given: it nulls the datasource and, while validating
		// sql, resolves sqlite datasources to in memory copies. From the cli that context dies with the
		// process. From a running app it is the request's own context, and the steps after the build
		// then hit an empty in memory "dev" instead of the real database. So the builder gets a context
		// of its own, with an empty memory stack, and the request keeps what it had.
		private PLangContext BuildContext()
		{
			var buildContext = context.Clone(MemoryStack.New(Container, engine), engine);
			buildContext.CallStack.EnterGoal(goal);
			return buildContext;
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
			return cdh.GetClassDescription(Type.GetType(type + ".Program"), methodName);

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

