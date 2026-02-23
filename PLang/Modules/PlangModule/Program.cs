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
using Actions = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.@this;
using R2Goal = PLang.Runtime2.Engine.Goals.Goal.@this;
using R2Step = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.@this;
using R2Action = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this;
using R2GoalCall = PLang.Runtime2.Engine.Goals.Goal.GoalCall;
using GoalMapper = PLang.Runtime2.Engine.Utility.GoalMapper;
using TypeMapping = PLang.Runtime2.Engine.Utility.TypeMapping;
using AppData = PLang.Runtime2.Engine.Utility.AppData;
using PLang.Runtime2.modules;
using PLang.SafeFileSystem;
using PLang.Utils;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
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



		[Description("Get all actions available from the registry")]
		public async Task<(Actions?, IError?)> GetActions()
		{
			var libraries = new EngineLibraries();

			var actions = new Actions(this.context);

			foreach (var ns in libraries.Modules)
			{
				foreach (var className in libraries.GetActions(ns))
				{
					var parameters = new List<Runtime2.Engine.Memory.Data>();
					System.Type? parameterType = null;

					// Try IAction-based handler first
					var handler = libraries.Get(ns, className);
					if (handler != null)
					{
						parameterType = handler.ParameterType;
					}
					else
					{
						// Fall back to [Action]-attributed type
						var actionType = libraries.GetActionType(ns, className);
						if (actionType == null) continue;
						parameterType = actionType;
					}

					if (parameterType != null)
					{
						var nCtx = new System.Reflection.NullabilityInfoContext();
						foreach (var prop in parameterType.GetProperties(
							System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
						{
							if (prop.Name == "EqualityContract" || prop.Name == "Context") continue;

							var typeName = Runtime2.Engine.Utility.TypeMapping.GetTypeName(prop.PropertyType);

							// Nullable reference types (value types already handled by TypeMapping)
							bool isNullable = Nullable.GetUnderlyingType(prop.PropertyType) != null;
							if (!isNullable && !prop.PropertyType.IsValueType)
								isNullable = nCtx.Create(prop).WriteState == System.Reflection.NullabilityState.Nullable;
							if (isNullable && !typeName.EndsWith("?"))
								typeName += "?";

							// ValidValues — append inline
							var validValues = Runtime2.Engine.Utility.TypeMapping.GetValidValues(prop.PropertyType);
							if (validValues != null)
								typeName += $"({string.Join("|", validValues)})";

							// @var marker
							var hasVar = prop.GetCustomAttribute<Runtime2.modules.VariableNameAttribute>() != null;

							// Default value
							var defaultAttr = prop.GetCustomAttribute<Runtime2.modules.DefaultAttribute>();

							// Build compact description: "@var string" or "actor(user|service|system) = \"user\""
							var desc = hasVar ? $"@var {typeName}" : typeName;
							if (defaultAttr != null)
								desc += $" = {FormatDefault(defaultAttr.Value)}";

							parameters.Add(new Runtime2.Engine.Memory.Data(prop.Name, desc));
						}
					}

					// Extract Cacheable from ActionAttribute
				bool cacheable = true;
				var actionType2 = libraries.GetActionType(ns, className);
				if (actionType2 != null)
				{
					var actionAttr = actionType2.GetCustomAttribute<Runtime2.modules.ActionAttribute>();
					if (actionAttr != null)
						cacheable = actionAttr.Cacheable;
				}

				actions.Add(new R2Action
					{
						Module = ns,
						ActionName = className,
						ParameterSchema = parameterType,
						Parameters = parameters,
						Cacheable = cacheable
					});
				}
			}

			return (actions, null);
		}


		[Description("Get goals formatted for Runtime2")]
		public async Task<(List<R2Goal>? Goals, IError? Error)> GetGoalsV2(string path, string parser)
		{
			var (goalsObj, error) = await GetGoals(path, visibility: "public_and_private", parser: parser);
			if (error != null) return (null, error);
			if (goalsObj is not List<Goal> v1Goals)
				return (null, new ProgramError("GetGoals did not return goals"));

			var runtime2Goals = v1Goals.Select(GoalMapper.ToRuntime2Goal).ToList();

			foreach (var r2Goal in runtime2Goals)
				MergeV2PrData(r2Goal);

			return (runtime2Goals, null);
		}

		private void MergeV2PrData(R2Goal goal)
		{
			var prPath = goal.PrPath;
			if (prPath == null || !fileSystem.File.Exists(prPath)) return;

			var prJson = fileSystem.File.ReadAllText(prPath);
			var prGoal = System.Text.Json.JsonSerializer.Deserialize<R2Goal>(
				prJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
			if (prGoal == null) return;

			for (int i = 0; i < goal.Steps.Count; i++)
			{
				var prStep = prGoal.Steps.FirstOrDefault(s => s.Text == goal.Steps[i].Text);
				if (prStep != null && prStep.Actions.Count > 0
					&& prStep.Actions.Any(a => a.Parameters.Count > 0))
				{
					goal.Steps[i].Actions = prStep.Actions;
				}

				if (prStep?.Cache != null)
					goal.Steps[i].Cache = prStep.Cache;
			}
		}

		[Description("Get type info for builder prompts")]
		public Task<(object?, IError?)> GetTypeInfo()
		{
			var names = TypeMapping.GetBuilderTypeNames();
			var schemas = TypeMapping.GetComplexTypeSchemas();
			var schemaLines = schemas.Select(kvp => $"  {kvp.Key}: {kvp.Value}");
			return Task.FromResult<(object?, IError?)>((
				new { TypeNames = string.Join(", ", names), TypeSchemas = string.Join("\n", schemaLines) }, null));
		}

		[Description("Validates actions from llm")]
		public async Task<(bool isValid, IError? Error)> ValidateActions(Actions actions)
		{
			if (actions == null || actions.Count == 0)
				return (false, new ProgramError("No actions provided", goalStep, function,
					Key: "NoActionsProvided"));

			var libraries = new EngineLibraries();

			var notFound = new List<string>();
			foreach (var action in actions)
			{
				if (!libraries.Contains(action.Module, action.ActionName))
					notFound.Add($"{action.Module}.{action.ActionName}");
			}

			if (notFound.Count > 0)
				return (false, new ProgramError(
					$"Actions not found: {string.Join(", ", notFound)}", goalStep, function,
					Key: "ActionNotFound"));

			// Resolve PrPath for goalcall parameters
			ResolveGoalCallPaths(actions);

			return (true, null);
		}

		private void ResolveGoalCallPaths(Actions actions)
		{
			foreach (var action in actions)
			{
				if (action.Parameters == null) continue;

				foreach (var param in action.Parameters)
				{
					if (!string.Equals(param.Type?.Value, "goal.call", StringComparison.OrdinalIgnoreCase))
						continue;

					var goalCall = DeserializeGoalCall(param.Value);
					if (goalCall == null || string.IsNullOrEmpty(goalCall.Name))
						continue;

					// Dynamic name (contains %variable%) — can't resolve at build time
					if (goalCall.Name.Contains('%'))
					{
						param.Value = goalCall;
						continue;
					}

					// Try prParser to find matching .pr file
					var prGoals = prParser.GetAllGoals();
					var matchedGoal = prGoals.FirstOrDefault(g =>
						g.GoalName.Equals(goalCall.Name, StringComparison.OrdinalIgnoreCase));
					if (matchedGoal != null)
					{
						goalCall.PrPath = matchedGoal.RelativePrPath;
						param.Value = goalCall;
						continue;
					}

					// Try goalParser to find .goal file and compute expected PrPath
					try
					{
						var goalFiles = goalParser.ParseGoalFile(goalCall.Name);
						if (goalFiles.Count > 0)
						{
							goalCall.PrPath = goalFiles[0].RelativePrPath;
							param.Value = goalCall;
							continue;
						}
					}
					catch
					{
						// Goal file not found — leave PrPath null
					}

					// Not found — leave PrPath null (runtime falls back to name lookup)
					param.Value = goalCall;
				}
			}
		}

		private static R2GoalCall? DeserializeGoalCall(object? value)
		{
			if (value is R2GoalCall gc)
				return gc;

			if (value is System.Text.Json.JsonElement je)
			{
				try
				{
					return System.Text.Json.JsonSerializer.Deserialize<R2GoalCall>(je.GetRawText(), new JsonSerializerOptions
					{
						PropertyNameCaseInsensitive = true
					});
				}
				catch { return null; }
			}

			if (value is string s)
			{
				// Plain string name — wrap in GoalCall
				return new R2GoalCall { Name = s };
			}

			return null;
		}

		[Description("Merges the llm step result to the step object")]
		public async Task<(R2Step? Step, IError? Error)> MergeStep(R2Step step, R2Step stepFromLlm)
		{
			if (step == null)
				return (null, new ProgramError("Step cannot be null", goalStep, function, Key: "MergeError"));
			if (stepFromLlm == null)
				return (null, new ProgramError("Step result from LLM cannot be null", goalStep, function, Key: "MergeError"));

			// Copy actions from LLM result to target step
			step.Actions.Clear();
			step.Actions.AddRange(stepFromLlm.Actions);

			// Copy cache from LLM result
			if (stepFromLlm.Cache != null)
				step.Cache = stepFromLlm.Cache;

			// Copy onError from LLM result
			if (stepFromLlm.OnError != null)
				step.OnError = stepFromLlm.OnError;

			// Copy errors/warnings from LLM result
			if (stepFromLlm.Errors.Count > 0)
			{
				step.Errors.Clear();
				step.Errors.AddRange(stepFromLlm.Errors);
			}
			if (stepFromLlm.Warnings.Count > 0)
			{
				step.Warnings.Clear();
				step.Warnings.AddRange(stepFromLlm.Warnings);
			}

			return (step, null);
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
				var fileProgram = GetProgramModule<FileModule.Program>();
				var files = await fileProgram.GetFilePathsInDirectory(fileOrFolderPath, includeSubfolders: true, searchPattern: "*.goal");
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

		[Description("Get available Runtime2 modules")]
		public async Task<(object?, IError?)> GetActions(string? format = null)
		{
			var libraries = new EngineLibraries();
			var result = new List<object>();

			foreach (var ns in libraries.Modules)
			{
				result.Add(new
				{
					Name = ns,
					Methods = libraries.GetActions(ns).ToList()
				});
			}

			if (format?.Equals("md", StringComparison.OrdinalIgnoreCase) == true)
			{
				var markdown = new System.Text.StringBuilder();
				foreach (var mod in result)
				{
					var jObj = Newtonsoft.Json.Linq.JObject.FromObject(mod);
					markdown.AppendLine($"## {jObj["Name"]}");
					markdown.AppendLine($"Methods: {string.Join(", ", jObj["Methods"]?.ToObject<List<string>>() ?? new())}");
					markdown.AppendLine();
				}
				return (markdown.ToString(), null);
			}

			return (result, null);
		}

		[Description("Load or create app.pr file with GUID id")]
		public async Task<(AppData?, IError?)> GetApp(string? appPath = null)
		{
			var path = GetPath(appPath ?? "");
			var buildPath = fileSystem.Path.Combine(path, ".build");
			var appPrPath = fileSystem.Path.Combine(buildPath, "app.pr");

			if (fileSystem.File.Exists(appPrPath))
			{
				try
				{
					var json = fileSystem.File.ReadAllText(appPrPath);
					var appData = System.Text.Json.JsonSerializer.Deserialize<AppData>(json, new JsonSerializerOptions
					{
						PropertyNameCaseInsensitive = true
					});
					return (appData, null);
				}
				catch (Exception ex)
				{
					return (null, new ProgramError($"Failed to read app.pr: {ex.Message}", goalStep, function));
				}
			}

			// Create new app.pr
			var newAppData = new AppData
			{
				Id = Guid.NewGuid().ToString(),
				Created = DateTime.UtcNow,
				Updated = DateTime.UtcNow,
				Name = fileSystem.Path.GetFileName(path),
				Version = "0.2"
			};

			try
			{
				if (!fileSystem.Directory.Exists(buildPath))
				{
					fileSystem.Directory.CreateDirectory(buildPath);
				}

				var json = System.Text.Json.JsonSerializer.Serialize(newAppData, new JsonSerializerOptions
				{
					WriteIndented = true,
					PropertyNamingPolicy = JsonNamingPolicy.CamelCase
				});
				fileSystem.File.WriteAllText(appPrPath, json);

				return (newAppData, null);
			}
			catch (Exception ex)
			{
				return (null, new ProgramError($"Failed to create app.pr: {ex.Message}", goalStep, function));
			}
		}

		[Description("Save app.pr file")]
		public async Task<(object?, IError?)> SaveApp(AppData app, string? path = null)
		{
			if (app == null)
			{
				return (null, new ProgramError("App cannot be null", goalStep, function));
			}

			try
			{
				app.Updated = DateTime.UtcNow;

				var appPath = path ?? ".build/app.pr";
				var absolutePath = GetPath(appPath);
				var directory = fileSystem.Path.GetDirectoryName(absolutePath);

				if (!string.IsNullOrEmpty(directory) && !fileSystem.Directory.Exists(directory))
				{
					fileSystem.Directory.CreateDirectory(directory);
				}

				var json = System.Text.Json.JsonSerializer.Serialize(app, new JsonSerializerOptions
				{
					WriteIndented = true,
					PropertyNamingPolicy = JsonNamingPolicy.CamelCase
				});
				fileSystem.File.WriteAllText(absolutePath, json);

				return (new { Path = absolutePath }, null);
			}
			catch (Exception ex)
			{
				return (null, new ProgramError($"Failed to save app: {ex.Message}", goalStep, function));
			}
		}

		[Description("Validate module, method and data")]
		public async Task<(object?, IError?)> Validate(string moduleName, string methodName, object? data)
		{
			var errors = new List<string>();

			// Validate module exists
			var moduleType = typeHelper.GetRuntimeType(moduleName);
			if (moduleType == null)
			{
				return (null, new ProgramError($"Module '{moduleName}' not found in registry", goalStep, function));
			}

			// Validate method exists
			var method = moduleType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
				.FirstOrDefault(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase));
			if (method == null)
			{
				return (null, new ProgramError($"Method '{methodName}' not found on module '{moduleName}'", goalStep, function));
			}

			// Basic data validation - just check it's not null if method has parameters
			var parameters = method.GetParameters();
			if (parameters.Length > 0 && data == null)
			{
				return (null, new ProgramError($"Method '{methodName}' requires parameters but data is null", goalStep, function));
			}

			return (new { IsValid = true, Module = moduleName, Method = methodName }, null);
		}

		[Description("Convert a Building.Model.Goal to R2Goal")]
		public async Task<(R2Goal?, IError?)> GetRuntime2Goal(Goal goal)
		{
			if (goal == null)
			{
				return (null, new ProgramError("Goal cannot be null", goalStep, function));
			}

			try
			{
				var runtime2Goal = Runtime2.Engine.Utility.GoalMapper.ToRuntime2Goal(goal);
				return await Task.FromResult((runtime2Goal, (IError?)null));
			}
			catch (Exception ex)
			{
				return (null, new ProgramError($"Failed to convert goal: {ex.Message}", goalStep, function));
			}
		}

		[Description("Save a Runtime2 goal as v0.2 .pr file (all steps in one file)")]
		public async Task<(object?, IError?)> SaveGoal(R2Goal goal)
		{
			if (goal == null)
			{
				return (null, new ProgramError("Goal cannot be null", goalStep, function));
			}

			try
			{
				// v0.2: Goal knows its own PrPath
				var prPath = goal.PrPath;
				var dir = fileSystem.Path.GetDirectoryName(prPath);
				if (!string.IsNullOrEmpty(dir) && !fileSystem.Directory.Exists(dir))
					fileSystem.Directory.CreateDirectory(dir);

				var json = System.Text.Json.JsonSerializer.Serialize(goal, new System.Text.Json.JsonSerializerOptions
				{
					WriteIndented = true,
					PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
					DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
				});

				await fileSystem.File.WriteAllTextAsync(prPath, json);

				return (new { Path = prPath, Format = "v0.2" }, null);
			}
			catch (Exception ex)
			{
				return (null, new ProgramError($"Failed to save goal: {ex.Message}", goalStep, function));
			}
		}

	

		[Description("Save a step/method .pr file")]
		public async Task<(object?, IError?)> SaveMethod(object methodPr)
		{
			if (methodPr == null)
			{
				return (null, new ProgramError("Method PR cannot be null", goalStep, function));
			}

			try
			{
				// The methodPr should contain path info and the method data
				var jObject = methodPr as JObject ?? JObject.FromObject(methodPr);

				var path = jObject["path"]?.ToString() ?? jObject["Path"]?.ToString();
				if (string.IsNullOrEmpty(path))
				{
					return (null, new ProgramError("Method PR must contain a 'path' property", goalStep, function));
				}

				var absolutePath = GetPath(path);
				var directory = fileSystem.Path.GetDirectoryName(absolutePath);

				if (!string.IsNullOrEmpty(directory) && !fileSystem.Directory.Exists(directory))
				{
					fileSystem.Directory.CreateDirectory(directory);
				}

				// Remove path from object before saving
				jObject.Remove("path");
				jObject.Remove("Path");

				var json = jObject.ToString(Formatting.Indented);
				fileSystem.File.WriteAllText(absolutePath, json);

				return (new { Path = absolutePath }, null);
			}
			catch (Exception ex)
			{
				return (null, new ProgramError($"Failed to save method: {ex.Message}", goalStep, function));
			}
		}

		[Description("Get methods for specified modules")]
		public async Task<(object?, IError?)> GetMethods(List<string> modules, string? format = null)
		{
			if (modules == null || modules.Count == 0)
			{
				return (null, new ProgramError("At least one module must be specified", goalStep, function));
			}

			var result = new List<object>();

			foreach (var moduleName in modules)
			{
				var (classDescription, error) = await GetClassDescription(moduleName);
				if (error != null)
				{
					continue; // Skip modules that don't exist
				}

				if (classDescription != null)
				{
					result.Add(new
					{
						Module = moduleName,
						Description = classDescription
					});
				}
			}

			if (format?.Equals("md", StringComparison.OrdinalIgnoreCase) == true)
			{
				var markdown = new System.Text.StringBuilder();
				foreach (var item in result)
				{
					var jObj = JObject.FromObject(item);
					markdown.AppendLine($"## {jObj["Module"]}");
					markdown.AppendLine();
					var desc = jObj["Description"] as JObject;
					if (desc != null)
					{
						markdown.AppendLine($"**Description:** {desc["Description"]}");
						markdown.AppendLine();
						var methods = desc["Methods"] as JArray;
						if (methods != null)
						{
							foreach (var method in methods)
							{
								markdown.AppendLine($"### {method["Name"]}");
								markdown.AppendLine($"{method["Description"]}");
								markdown.AppendLine();
							}
						}
					}
				}
				return (markdown.ToString(), null);
			}

			return (result, null);
		}

		[Description("Validate goal structure")]
		public async Task<(object?, IError?)> ValidateGoal(Goal goal)
		{
			if (goal == null)
			{
				return (null, new ProgramError("Goal cannot be null", goalStep, function));
			}

			var errors = new List<string>();
			var warnings = new List<string>();

			// Validate goal name
			if (string.IsNullOrWhiteSpace(goal.GoalName))
			{
				errors.Add("Goal name is required");
			}

			// Validate steps
			if (goal.GoalSteps == null || goal.GoalSteps.Count == 0)
			{
				warnings.Add("Goal has no steps");
			}
			else
			{
				for (int i = 0; i < goal.GoalSteps.Count; i++)
				{
					var step = goal.GoalSteps[i];
					if (string.IsNullOrWhiteSpace(step.Text))
					{
						errors.Add($"Step {i + 1}: Text is required");
					}
					if (string.IsNullOrWhiteSpace(step.ModuleType))
					{
						warnings.Add($"Step {i + 1}: Module type not set");
					}
				}
			}

			var isValid = errors.Count == 0;

			return (new
			{
				IsValid = isValid,
				Errors = errors,
				Warnings = warnings
			}, null);
		}

		[Description("Validate step method/function")]
		public async Task<(object?, IError?)> ValidateMethod(GoalStep step, object function)
		{
			if (step == null)
			{
				return (null, new ProgramError("Step cannot be null", goalStep, this.function));
			}

			var errors = new List<string>();
			var warnings = new List<string>();

			// Validate module exists
			if (!string.IsNullOrEmpty(step.ModuleType))
			{
				var moduleType = typeHelper.GetRuntimeType(step.ModuleType);
				if (moduleType == null)
				{
					errors.Add($"Module '{step.ModuleType}' not found");
				}
				else if (!string.IsNullOrEmpty(step.Name))
				{
					// Validate method exists
					var method = moduleType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
						.FirstOrDefault(m => m.Name.Equals(step.Name, StringComparison.OrdinalIgnoreCase));
					if (method == null)
					{
						errors.Add($"Method '{step.Name}' not found in module '{step.ModuleType}'");
					}
				}
			}

			// Validate function object if provided
			if (function != null)
			{
				var jFunc = function as JObject ?? JObject.FromObject(function);
				var functionName = jFunc["FunctionName"]?.ToString() ?? jFunc["Name"]?.ToString();
				if (string.IsNullOrEmpty(functionName))
				{
					warnings.Add("Function name not specified");
				}
			}

			var isValid = errors.Count == 0;

			return (new
			{
				IsValid = isValid,
				Errors = errors,
				Warnings = warnings
			}, null);
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
			var error = await builder.Start(Container, context, step.Goal.AbsoluteGoalPath);

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

		[Description("Builds(compiles) a goal in plang code")]
		public async Task<List<IBuilderError>?> BuildPlangCode(Goal goal)
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

		private static string FormatDefault(object? value) => value switch
		{
			null => "null",
			string s => $"\"{s}\"",
			bool b => b ? "true" : "false",
			_ => value.ToString() ?? "null"
		};
	}
}

