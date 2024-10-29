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
using Newtonsoft.Json.Linq;
using PLang.Models;
using System.Collections;
using PLang.Utils;

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
		}
	
		public async Task<IError?> IsNotEmpty(object? item1, GoalToCall? goalToCallIfTrue = null, Dictionary<string, object?>? parametersForGoalIfTrue = null, GoalToCall? goalToCallIfFalse = null, Dictionary<string, object?>? parametersForGoalIfFalse = null, bool ignoreCase = false)
		{
			var result = !IsEmptyCheck(item1);

			return await ExecuteResult(result, goalToCallIfTrue, parametersForGoalIfTrue, goalToCallIfFalse, parametersForGoalIfFalse);
		}
		public async Task<IError?> IsEmpty(object? item1, GoalToCall? goalToCallIfTrue = null, Dictionary<string, object?>? parametersForGoalIfTrue = null, GoalToCall? goalToCallIfFalse = null, Dictionary<string, object?>? parametersForGoalIfFalse = null, bool ignoreCase = false)
		{
			var result = IsEmptyCheck(item1);

			return await ExecuteResult(result, goalToCallIfTrue, parametersForGoalIfTrue, goalToCallIfFalse, parametersForGoalIfFalse);
		}

		private bool IsEmptyCheck(object? item1)
		{
			if (item1 == null) return true;
			var result = false;

			if (item1 is string str)
			{
				result = string.IsNullOrWhiteSpace(str);
			}
			else if (item1 is IList)
			{
				result = ((IList)item1).Count > 0;
			}
			else if (item1 is IDictionary)
			{
				result = ((IDictionary)item1).Count > 0;
			}
			else
			{
				result = string.IsNullOrWhiteSpace(item1.ToString());
			}
			return result;

		}

		public async Task<IError?> IsEqual(object item1, object item2, GoalToCall? goalToCallIfTrue = null, Dictionary<string, object?>? parametersForGoalIfTrue = null, GoalToCall? goalToCallIfFalse = null, Dictionary<string, object?>? parametersForGoalIfFalse = null, bool ignoreCase = false)
		{
			var result = false;
			if (item1 is JObject jobj)
			{
				item1 = jobj.ToString();
			}

			if (item2 is JObject jobj2)
			{
				item2 = jobj2.ToString();
			}

			if (item1 is string i1 && item2 is string i2)
			{
				StringComparison co = (ignoreCase) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
				result = i1.Equals(i2, co);
			}
			else if (item1 is double double1 && item2 is double double2)
			{
				result = double1 == double2;
			}
			else if (item1 is int int1 && item2 is int int2)
			{
				result = int1 == int2;
			}
			else if (item1 is bool bool1 && item2 is bool bool2)
			{
				result = bool1 == bool2;
			}
			else
			{
				result = Equals(item1, item2);
			}

			return await ExecuteResult(result, goalToCallIfTrue, parametersForGoalIfTrue, goalToCallIfFalse, parametersForGoalIfFalse);


		}

		// 
		[Description("The expression could be \"%data.count% > 1000\", \"true\", \"false\", \"%data% contains 'ble.com'\"")]
		public async Task<IError?> Evaluate(object data, string expression, GoalToCall? goalToCallIfTrue = null, Dictionary<string, object?>? parametersForGoalIfTrue = null, GoalToCall? goalToCallIfFalse = null, Dictionary<string, object?>? parametersForGoalIfFalse = null)
		{
			var cond = new Condition(expression);
			var result = false;
			if (data is JObject jobj)
			{
				result = cond.Evaluate(jobj);
			}
			if (data is JArray jarr)
			{
				result = cond.Evaluate(jarr);
			}
			return await ExecuteResult(result, goalToCallIfTrue, parametersForGoalIfTrue, goalToCallIfFalse, parametersForGoalIfFalse);
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
					foreach (var parameter in parameters)
					{
						var parameterType = parameters[idx++].ParameterType;
						if (parameterType.FullName == "PLang.SafeFileSystem.PLangFileSystem")
						{
							parametersObject.Add(fileSystem);
						}
						else
						{
							var inputParam = answer.InputParameters.FirstOrDefault(p => p.ParameterName == parameter.Name);
							if (inputParam != null)
							{
								var value = memoryStack.Get(inputParam.VariableName, parameterType);
								parametersObject.Add(value);
							}
						}
					}
				}

				// The first parameter is the instance you want to call the method on. For static methods, you should pass null.
				// The second parameter is an object array containing the arguments of the method.
				bool result = (bool?)method.Invoke(null, parametersObject.ToArray()) ?? false;

				return await ExecuteResult(result, answer.GoalToCallOnTrue, answer.GoalToCallOnTrueParameters, answer.GoalToCallOnFalse, answer.GoalToCallOnFalseParameters);

			}
			catch (Exception ex)
			{
				var error = CodeExceptionHandler.GetError(ex, answer, goalStep);
				return error;
			}

		}

		private async Task<IError?> ExecuteResult(bool result, GoalToCall? goalToCallOnTrue, Dictionary<string, object?>? goalToCallOnTrueParameters, GoalToCall? goalToCallOnFalse, Dictionary<string, object?>? goalToCallOnFalseParameters)
		{
			Task<(IEngine, IError? error)>? task = null;
			if (result && goalToCallOnTrue != null && goalToCallOnTrue.Value != null)
			{
				if (VariableHelper.IsVariable(goalToCallOnTrue))
				{
					goalToCallOnTrue = variableHelper.LoadVariables(goalToCallOnTrue)?.ToString();
				}
				if (goalToCallOnTrueParameters?.Count == 1 && VariableHelper.IsVariable(goalToCallOnTrueParameters.FirstOrDefault().Value))
				{
					var obj = variableHelper.LoadVariables(goalToCallOnTrueParameters.FirstOrDefault().Value);
					if (obj is JObject jObject)
					{
						goalToCallOnTrueParameters = jObject.ToDictionary();
					}
				}
				task = pseudoRuntime.RunGoal(engine, context, goal.RelativeAppStartupFolderPath, goalToCallOnTrue, goalToCallOnTrueParameters, goal);
			}
			else if (!result && goalToCallOnFalse != null && goalToCallOnFalse.Value != null)
			{
				if (VariableHelper.IsVariable(goalToCallOnFalse))
				{
					goalToCallOnFalse = variableHelper.LoadVariables(goalToCallOnFalse)?.ToString();
				}
				
				if (goalToCallOnFalseParameters?.Count == 1 && VariableHelper.IsVariable(goalToCallOnFalseParameters.FirstOrDefault().Value))
				{
					var obj = variableHelper.LoadVariables(goalToCallOnFalseParameters.FirstOrDefault().Value);
					if (obj is JObject jObject)
					{
						goalToCallOnFalseParameters = jObject.ToDictionary();
					}
				}
				task = pseudoRuntime.RunGoal(engine, context, goal.RelativeAppStartupFolderPath, goalToCallOnFalse, goalToCallOnFalseParameters, goal);
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

			
				var nextStep = goalStep.NextStep;
				if (nextStep == null) return null;

				bool isIndent = (goalStep.Indent + 4 == nextStep.Indent);

				while (isIndent)
				{
					nextStep.Execute = result && (goalStep.Indent + 4 == nextStep.Indent);

					nextStep = nextStep.NextStep;
					if (nextStep == null) break;
					isIndent = (goalStep.Indent < nextStep.Indent);
				}
			
			return null;
		}
	}
}

