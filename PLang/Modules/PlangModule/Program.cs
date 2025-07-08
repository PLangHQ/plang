using LightInject;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
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

		[Description("Get goals in file or folder. visiblity is either public|public_and_private|private")]
		public async Task<(object? Goals, IError? Error)> GetGoals(string fileOrFolderPath, string visibility = "public", List<string>? propertiesToExtract = null)
		{
			List<Goal> goals = new List<Goal>();
			string path = GetPath(fileOrFolderPath);
			if (path.EndsWith(".goal"))
			{
				goals = prParser.GetAllGoals().Where(p => p.AbsoluteGoalPath.Equals(path, StringComparison.OrdinalIgnoreCase)).ToList();
			}
			else if (fileSystem.Directory.Exists(fileOrFolderPath))
			{
				goals = prParser.GetAllGoals().Where(p => p.AbsoluteGoalFolderPath.StartsWith(path, StringComparison.OrdinalIgnoreCase)).ToList();
			}else
			{
				return (null, new ProgramError($"The path {fileOrFolderPath} could not be found, searched for it at {path}"));
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

			if (propertiesToExtract?.Count == 0) return (goals, null);

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

			var goals = (List<Goal>) result.Goals;
			return (goals.Where(p => p.IsSetup).ToList(), null);
		}


		public async Task<(string?, IError?)> GetModules(string stepText, List<string> excludeModules)
		{
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

		[Description("Returns the method description in a module for specific method")]
		public async Task<(MethodDescription?, IError?)> GetMethodDescription(string moduleName, string methodName)
		{
			var programType = typeHelper.GetRuntimeType(moduleName);

			var classDescriptionHelper = new ClassDescriptionHelper();
			var result = classDescriptionHelper.GetMethodDescription(programType, methodName);

			return (result.MethodDescription, result.Error);
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
		 * TODO: Dictionary<string, object?> Parameters should be Parameters class, or just parameter, that is better 
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

			var variables = variableHelper.GetVariables(step.Text);
			return variables;
		}

		public async Task<(List<GoalStep>?, IError?)> GetSteps(string goalPath)
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
			var startingEngine = engine.GetContext()[ReservedKeywords.StartingEngine] as IEngine;
			if (startingEngine == null) startingEngine = engine;

			//engine.GetContext().Remove(ReservedKeywords.IsEvent);

			fileAccessHandler.GiveAccess(fileSystem.OsDirectory, fileSystem.GoalsPath);
			if (parameters != null)
			{
				var ms = engine.GetMemoryStack();
				foreach (var parameter in parameters)
				{
					ms.Put(parameter.Key, parameter.Value);
				}
			}
			var result = await startingEngine.ProcessPrFile(step.Goal, step, step.Number);
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
			fileAccessHandler.GiveAccess(fileSystem.OsDirectory, fileSystem.GoalsPath);
			if (!fileSystem.File.Exists(absoluteFilePath))
			{
				return (null, new ProgramError($"The file {prFileName} could not be found. I searched for it at {absoluteFilePath}", goalStep, function));
			}
			var startingEngine = engine.GetContext()[ReservedKeywords.StartingEngine] as IEngine;
			if (startingEngine == null) startingEngine = engine;

			engine.GetEventRuntime().SetActiveEvents(new());

			var result = await startingEngine.RunFromStep(absoluteFilePath);
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
			var error = await builder.Start(Container, step.Goal.AbsoluteGoalPath);

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
			var error = await builder.Start(Container, goal.AbsoluteGoalPath);

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

