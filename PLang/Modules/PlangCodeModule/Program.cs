using LightInject;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using PLang.Attributes;
using PLang.Building;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Errors.Runtime;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Utils;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using static PLang.Modules.BaseBuilder;

namespace PLang.Modules.PlangCodeModule
{
	[Description("Compiles plang code, gets goals from .goal files, method descriptions and class scheme")]
	public class Program : BaseProgram
	{
		private readonly ILogger logger;
		private readonly IGoalParser goalParser;
		private readonly IPLangFileSystem fileSystem;
		private readonly IEngine engine;
		private readonly IGoalBuilder goalBuilder;
		private readonly PrParser prParser;

		public Program(ILogger logger, IGoalParser goalParser, IPLangFileSystem fileSystem, IEngine engine, IGoalBuilder goalBuilder, PrParser prParser) : base()
		{
			this.logger = logger;
			this.goalParser = goalParser;
			this.fileSystem = fileSystem;
			this.engine = engine;
			this.goalBuilder = goalBuilder;
			this.prParser = prParser;
		}

		[Description("Get goals in file or folder. visiblity is either public|public_and_private|private")]
		public async Task<object> GetGoals(string filePath, string visiblity = "public", string[]? fields = null)
		{
			List<Goal> goals = new List<Goal>();
			string path = GetPath(filePath);
			if (path.EndsWith(".goal"))
			{
				goals = goalParser.ParseGoalFile(path);
			}
			else
			{
				var files = fileSystem.Directory.GetFiles(path);
				foreach (var file in files)
				{
					goals.AddRange(goalParser.ParseGoalFile(file));
				}
			}
			if (visiblity == "public")
			{
				goals = goals.Where(p => p.Visibility == Visibility.Public).ToList();
			}
			if (visiblity == "private")
			{
				goals = goals.Where(p => p.Visibility == Visibility.Private).ToList();
			}
			if (fields == null) return goals;

			JArray array = new JArray();
			foreach (var goal in goals)
			{
				var jObject = new JObject();
				foreach (var field in fields)
				{

					var property = goal.GetType().GetProperties().FirstOrDefault(p => p.Name.Equals(field, StringComparison.OrdinalIgnoreCase));
					if (property != null)
					{
						var value = property.GetValue(goal);
						if (value != null)
						{
							jObject.Add(field, value.ToString());
						}
					}

				}
				array.Add(jObject);
			}


			return array;
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

		public async Task<string> GetMethods(string moduleName)
		{
			var programType = typeHelper.GetRuntimeType(moduleName);
			var methods = typeHelper.GetMethodNamesAsString(programType);
			return methods;
		}

		public async Task<(MethodDescription?, IError?)> GetMethodDescription(string moduleName, string methodName)
		{
			var programType = typeHelper.GetRuntimeType(moduleName);
			return TypeHelper.GetMethodDescription(programType, methodName);
		}

		public async Task<string> GetMethodMappingScheme()
		{
			string scheme = TypeHelper.GetJsonSchema(typeof(MethodExecution));
			return scheme;
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

		public async Task<IError?> RunStep(string prFileName)
		{
			if (string.IsNullOrEmpty(prFileName))
			{
				return new ProgramError($"prFileName is empty. I cannot run a step if I don't know what to run.", goalStep, function,
					FixSuggestion: "Something has broke between the IDE sending the information and the runtime. Check if SendDebug.goal and the IDE is talking together correctly.");
			}
			var absolutePrFileName = fileSystem.Path.Join(fileSystem.GoalsPath, prFileName);
			if (!fileSystem.File.Exists(absolutePrFileName))
			{
				return new ProgramError($"The file {prFileName} could not be found. I looked for it at {absolutePrFileName}", goalStep, function);
			}
			var startingEngine = engine.GetContext()[ReservedKeywords.StartingEngine] as IEngine;
			if (startingEngine == null) startingEngine = engine;
			engine.GetContext().Remove(ReservedKeywords.IsEvent);

			var result = await startingEngine.RunFromStep(absolutePrFileName);
			return result;
		}
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
			var goal = goals.FirstOrDefault(p => p.AbsoluteGoalPath == step.Goal.AbsoluteGoalPath);
			if (goal != null)
			{
				step = goal.GoalSteps.FirstOrDefault(p => p.Number == step.Number);
			}

			return (step, error);
		}
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
	}
}

