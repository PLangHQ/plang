using Newtonsoft.Json;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Runtime;
using System.ComponentModel;
using System.Reflection;
using static PLang.Services.CompilerService.CSharpCompiler;
using static PLang.Modules.ConditionalModule.Builder;
using PLang.Services.CompilerService;
using static PLang.Runtime.Startup.ModuleLoader;
using PLang.Errors;
using PLang.Errors.Runtime;

namespace PLang.Modules.ConditionalModule
{
	[Description(@"Manages if conditions for the user request. Example 1:'if %isValid% is true then', this condition would return true if %isValid% is true. Example 2:'if %address% is empty then', this would check if the %address% variable is empty and return true if it is, else false. Use when checking if file or directory exists.")]
	public class Program : BaseProgram
	{
		private readonly IEngine engine;
		private readonly IPseudoRuntime pseudoRuntime;
		private readonly IPLangFileSystem fileSystem;

		public Program(IEngine engine, IPseudoRuntime pseudoRuntime, IPLangFileSystem fileSystem) : base()
		{
			this.engine = engine;
			this.pseudoRuntime = pseudoRuntime;
			this.fileSystem = fileSystem;
		}
		/*
		 * 
		 * Needs more change then expected, leaving it commented out
		 * 
		public async Task<bool> FileExists(string filePathOrVariableName)
		{
			return fileSystem.File.Exists(filePathOrVariableName);
		}

		public async Task<bool> DirectoryExists(string dirPathOrVariableName)
		{
			return fileSystem.File.Exists(dirPathOrVariableName);
		}
		public async Task<bool> HasAccessToPath(string dirOrFilePathOrVariableName)
		{
			return fileSystem.ValidatePath(dirOrFilePathOrVariableName) != null;
		}*/

		public override async Task<IError?> Run()
		{

			var answer = JsonConvert.DeserializeObject<Implementation>(instruction.Action.ToString());
			try
			{
				string dllName = goalStep.PrFileName.Replace(".pr", ".dll");
				Assembly? assembly = Assembly.LoadFile(Path.Combine(Goal.AbsolutePrFolderPath, dllName));
				if (assembly == null)
				{
					return new StepError($"Could not find {dllName}. Stopping execution for step {goalStep.Text}", goalStep);
				}
				Type? type = assembly.GetType(answer.Namespace + "." + answer.Name);
				if (type == null)
				{
					return new StepError($"Could not find type {answer.Name}. Stopping execution for step {goalStep.Text}", goalStep);
				}
				MethodInfo? method = type.GetMethod("ExecutePlangCode");
				if (method == null)
				{
					return new StepError($"Method 'ExecutePlangCode' could not be found in {answer.Name}. Stopping execution for step {goalStep.Text}", goalStep);
				}
				var parameters = method.GetParameters();

				var parametersObject = new List<object?>();
				int idx = 0;
				if (answer.InputParameters != null)
				{
					foreach (var parameter in answer.InputParameters)
					{
						var parameterType = parameters[idx++].ParameterType;
						if (parameterType.FullName == "PLang.SafeFileSystem.PLangFileSystem")
						{
							parametersObject.Add(fileSystem);
						}
						else
						{
							var key = parameter.Key;
							if (key.ToLower().StartsWith("settings."))
							{
								key = "%" + key + "%";
							}
							var value = memoryStack.Get(key, parameterType);
							parametersObject.Add(value);
						}
					}
				}

				// The first parameter is the instance you want to call the method on. For static methods, you should pass null.
				// The second parameter is an object array containing the arguments of the method.
				bool result = (bool)method.Invoke(null, parametersObject.ToArray());
				Task<(IEngine, IError? error)>? task = null;
				if (result && answer.GoalToCallOnTrue != null)
				{
					Dictionary<string, object?>? param = answer.GoalToCallOnTrueParameters;
					task = pseudoRuntime.RunGoal(engine, context, goal.RelativeAppStartupFolderPath, answer.GoalToCallOnTrue, param, goal);
				}
				else if (!result && answer.GoalToCallOnFalse != null)
				{
					Dictionary<string, object?>? param = answer.GoalToCallOnFalseParameters;
					task = pseudoRuntime.RunGoal(engine, context, goal.RelativeAppStartupFolderPath, answer.GoalToCallOnFalse, param, goal);
				}

				if (task != null)
				{
					try
					{
						var taskExecuted = await task;
						if (taskExecuted.error != null) return taskExecuted.error;

					}
					catch { }

					if (task.IsFaulted)
					{
						throw task.Exception;
					}
				}

				if (result)
				{
					var nextStep = goalStep.NextStep;
					if (nextStep == null) return null;

					bool isIndent = (goalStep.Indent + 4 == nextStep.Indent);

					while (isIndent)
					{
						nextStep.Execute = true;

						nextStep = nextStep.NextStep;
						if (nextStep == null) break;
						isIndent = (goalStep.Indent + 4 == nextStep.Indent);
					}
				}
				return null;
			}
			catch (Exception ex)
			{
				var error = CodeExceptionHandler.GetError(ex, answer, goalStep);
				return error;
			}

		}

	}
}

