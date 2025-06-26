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
using Microsoft.Extensions.Logging;
using PLang.Services.OutputStream;
using PLang.Modules.ThrowErrorModule;
using static PLang.Modules.ConditionalModule.ConditionEvaluator;

namespace PLang.Modules.ConditionalModule
{
	[Description(@"Manages if conditions for the user request. Example 1:'if %isValid% is true then call SomeGoal, else call OtherGoal', this condition would return true if %isValid% is true and call a goals on either conditions. Example 2:'if %address% is empty then', this would check if the %address% variable is empty and return true if it is, else false. Use when checking if file or directory exists. Prefer predefined methods over SimpleCondition and CompoundCondition")]
	public class Program : BaseProgram
	{
		private readonly IEngine engine;
		private readonly IPseudoRuntime pseudoRuntime;
		private readonly IPLangFileSystem fileSystem;
		private readonly ILogger logger;

		public Program(IEngine engine, IPseudoRuntime pseudoRuntime, IPLangFileSystem fileSystem, ILogger logger) : base()
		{
			this.engine = engine;
			this.pseudoRuntime = pseudoRuntime;
			this.fileSystem = fileSystem;
			this.logger = logger;
		}

		public async Task<(bool, IError?)> FileExists(string filePathOrVariableName, GoalToCallInfo? goalToCallIfTrue = null, GoalToCallInfo? goalToCallIfFalse = null,
			ErrorInfo? throwErrorOnTrue = null, ErrorInfo? throwErrorOnFalse = null)
		{
			string path = GetPath(filePathOrVariableName);
			var result = fileSystem.File.Exists(path);
			return (result, await ExecuteResult(result, goalToCallIfTrue, goalToCallIfFalse, throwErrorOnTrue, throwErrorOnFalse));
		}

		public async Task<(bool, IError?)> DirectoryExists(string dirPathOrVariableName, GoalToCallInfo? goalToCallIfTrue = null, GoalToCallInfo? goalToCallIfFalse = null,
			ErrorInfo? throwErrorOnTrue = null, ErrorInfo? throwErrorOnFalse = null)
		{
			var path = GetPath(dirPathOrVariableName);
			var result = fileSystem.File.Exists(path);
			return (result, await ExecuteResult(result, goalToCallIfTrue, goalToCallIfFalse, throwErrorOnTrue, throwErrorOnFalse));
		}
		public async Task<(bool, IError?)> HasAccessToPath(string dirOrFilePathOrVariableName, GoalToCallInfo? goalToCallIfTrue = null, GoalToCallInfo? goalToCallIfFalse = null,
			ErrorInfo? throwErrorOnTrue = null, ErrorInfo? throwErrorOnFalse = null)
		{
			var path = GetPath(dirOrFilePathOrVariableName);
			var result = fileSystem.ValidatePath(path) != null;
			return (result, await ExecuteResult(result, goalToCallIfTrue, goalToCallIfFalse, throwErrorOnTrue, throwErrorOnFalse));
		}

		[Description("Operator: ==|!=|<|>|<=|>=|in|contains|startswith|endswith|indexOf. IsNot property indicates if the condition is a negation of the specified operator. True for ‘is not’, ‘does not’, etc.")]
		public async Task<(bool, IError?)> SimpleCondition(SimpleCondition condition, 
			GoalToCallInfo? goalToCallIfTrue = null, GoalToCallInfo? goalToCallIfFalse = null,
			ErrorInfo? throwErrorOnTrue = null, ErrorInfo? throwErrorOnFalse = null)
		{
			var result = ConditionEngine.Evaluate(condition);
			return (result, await ExecuteResult(result, goalToCallIfTrue, goalToCallIfFalse, throwErrorOnTrue, throwErrorOnFalse));
		}

		[Description("Operator: ==|!=|<|>|<=|>=|in|contains|startswith|endswith|indexOf.  IsNot property indicates if the condition is a negation of the specified operator. True for ‘is not’, ‘does not’, etc.")]
		public async Task<(bool, IError?)> CompoundCondition(CompoundCondition condition, 
			GoalToCallInfo? goalToCallIfTrue = null, GoalToCallInfo? goalToCallIfFalse = null,
			ErrorInfo? throwErrorOnTrue = null, ErrorInfo? throwErrorOnFalse = null)
		{
			var result = ConditionEngine.Evaluate(condition);
			return (result, await ExecuteResult(result, goalToCallIfTrue, goalToCallIfFalse, throwErrorOnTrue, throwErrorOnFalse));
		}
		
		public async Task<(bool, IError?)> IsFalse(bool? item, GoalToCallInfo? goalToCallIfTrue = null,
			GoalToCallInfo? goalToCallIfFalse = null,
			ErrorInfo? throwErrorOnTrue = null, ErrorInfo? throwErrorOnFalse = null)
		{
			if (item == null) { item = false; }
			item = !item;

			return (item.Value, await ExecuteResult(item.Value, goalToCallIfTrue, goalToCallIfFalse, throwErrorOnTrue, throwErrorOnFalse));
		}

		public async Task<(bool, IError?)> IsTrue(bool? item, GoalToCallInfo? goalToCallIfTrue = null,
			GoalToCallInfo? goalToCallIfFalse = null,
			ErrorInfo? throwErrorOnTrue = null, ErrorInfo? throwErrorOnFalse = null)
		{
			if (item == null) { item = false; }

			return (item.Value, await ExecuteResult(item.Value, goalToCallIfTrue, goalToCallIfFalse, throwErrorOnTrue, throwErrorOnFalse));
		}
		public async Task<(bool, IError?)> IsNotEmpty(object? item, GoalToCallInfo? goalToCallIfTrue = null,
			GoalToCallInfo? goalToCallIfFalse = null,
			ErrorInfo? throwErrorOnTrue = null, ErrorInfo? throwErrorOnFalse = null)
		{
			var result = !IsEmptyCheck(item);

			return (result, await ExecuteResult(result, goalToCallIfTrue, goalToCallIfFalse, throwErrorOnTrue, throwErrorOnFalse));
		}

		 
		public async Task<(bool, IError?)> IsEmpty(object? item, GoalToCallInfo? goalToCallIfTrue = null,
						GoalToCallInfo? goalToCallIfFalse = null,
						ErrorInfo? throwErrorOnTrue = null, ErrorInfo? throwErrorOnFalse = null)
		{
			var result = IsEmptyCheck(item);
			return (result, await ExecuteResult(result, goalToCallIfTrue, goalToCallIfFalse, throwErrorOnTrue, throwErrorOnFalse));
		}
		public async Task<(bool?, IError?)> ContainsNumbers(object? item, List<int> contains, GoalToCallInfo? goalToCallIfTrue = null,  GoalToCallInfo? goalToCallIfFalse = null, 
			ErrorInfo? throwErrorOnTrue = null, ErrorInfo? throwErrorOnFalse = null)
		{
			bool? result = null;
			if (item == null) result = false;
			if (item is int i)
			{
				result = contains.Any(p => p == i);
			}

			if (item is List<int> items)
			{
				result = contains.Any(p => items.Any(i => i == p));
			}

			if (result == null)
			{
				return (null, new ProgramError($"object is type of '{item?.GetType()}'. Not sure how I should find {contains} in it."));
			}

			return (result, await ExecuteResult(result.Value, goalToCallIfTrue, goalToCallIfFalse, throwErrorOnTrue, throwErrorOnFalse));

		}
		public async Task<(bool?, IError?)> ContainsString(object? item, string contains, GoalToCallInfo? goalToCallIfTrue = null, GoalToCallInfo? goalToCallIfFalse = null,
			ErrorInfo? throwErrorOnTrue = null, ErrorInfo? throwErrorOnFalse = null)
		{
			bool? result = null;
			if (item == null) result = false;

			if (item is string str)
			{
				result = str.Contains(contains.ToString());
			}

			if (result != null)
			{
				return (result, await ExecuteResult(result.Value, goalToCallIfTrue, goalToCallIfFalse, throwErrorOnTrue, throwErrorOnFalse));
			}

			return (null, new ProgramError($"object is type of '{item?.GetType()}'. Not sure how I should find {contains} in it.{ErrorReporting.CreateIssueNotImplemented}"));
		}

		private bool IsEmptyCheck(object? item)
		{
			if (item == null) return true;
			if (item is ObjectValue ov)
			{
				if (!ov.Initiated) return true;
				return ov.IsEmpty;
			}

			var result = false;

			if (item is string str)
			{
				result = string.IsNullOrWhiteSpace(str);
			}
			else if (item is IList)
			{
				result = ((IList)item).Count == 0;
			}
			else if (item is IDictionary)
			{
				result = ((IDictionary)item).Count == 0;
			}
			else
			{
				result = string.IsNullOrWhiteSpace(item.ToString());
			}
			return result;

		}

		public async Task<(bool? Result, IError? Error)> IsEqual(object? item1, object? item2, GoalToCallInfo? goalToCallIfTrue = null,
			GoalToCallInfo? goalToCallIfFalse = null, bool ignoreCase = true,
			ErrorInfo? throwErrorOnTrue = null, ErrorInfo? throwErrorOnFalse = null)
		{
			bool? result = null;
			if (item1 == item2) result = true;
			if (result == null && item1 != null && item1.Equals(item2)) result = true;

			if (result == null)
			{
				if (item1 is JObject jobj)
				{
					item1 = jobj.ToString();
				}

				if (item2 is JObject jobj2)
				{
					item2 = jobj2.ToString();
				}
				if (item1 is string && item2 is not string)
				{
					item2 = item2?.ToString() ?? string.Empty;
				}

				if (item1?.GetType() != item2?.GetType())
				{

					(item1, item2) = TypeHelper.TryConvertToMatchingType(item1, item2);

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
			}

			return (result, await ExecuteResult(result.Value, goalToCallIfTrue, goalToCallIfFalse, throwErrorOnTrue, throwErrorOnFalse));

		}


		/*
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

		[Description("Choose RunInlineCode when no other method matches user intent. Implementation variable can be set to null.")]
		public async Task<(object?, IError?)> RunInlineCode(ConditionImplementationResponse implementation)
		{

			try
			{
				string dllName = goalStep.PrFileName.Replace(".pr", ".dll");
				Assembly? assembly = Assembly.LoadFile(Path.Join(Goal.AbsolutePrFolderPath, dllName));
				if (assembly == null)
				{
					return (null, new StepError($"Could not find {dllName}. Stopping execution for step {goalStep.Text}", goalStep));
				}
				Type? type = assembly.GetType(implementation.Namespace + "." + implementation.Name);
				if (type == null)
				{
					return (null, new StepError($"Could not find type {implementation.Name}. Stopping execution for step {goalStep.Text}", goalStep));
				}
				MethodInfo? method = type.GetMethod("ExecutePlangCode");
				if (method == null)
				{
					return (null, new StepError($"Method 'ExecutePlangCode' could not be found in {implementation.Name}. Stopping execution for step {goalStep.Text}", goalStep));
				}
				var parameters = method.GetParameters();

				var parametersObject = new List<object?>();
				int idx = 0;
				if (implementation.Parameters != null)
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
							var inputParam = implementation.Parameters.FirstOrDefault(p => p.Name == parameter.Name);
							if (inputParam != null)
							{
								var value = memoryStack.Get(inputParam.Name, parameterType);
								parametersObject.Add(value);
							}
						}
					}
				}
				logger.LogTrace("Parameters:{0}", parametersObject);

				// The first parameter is the instance you want to call the method on. For static methods, you should pass null.
				// The second parameter is an object array containing the arguments of the method.
				bool result = (bool?)method.Invoke(null, parametersObject.ToArray()) ?? false;

				return (result, await ExecuteResult(result, implementation.GoalToCallOnTrue, implementation.GoalToCallOnFalse));

			}
			catch (Exception ex)
			{
				var error = CodeExceptionHandler.GetError(ex, implementation, goalStep);
				return (null, error);
			}

		}

		private async Task<IError?> ExecuteResult(bool result, GoalToCallInfo? goalToCallOnTrue,
			GoalToCallInfo? goalToCallOnFalse, ErrorInfo? throwErrorOnTrue = null, ErrorInfo? throwErrorOnFalse = null)
		{

			Task<(IEngine, object? Variables, IError? error, IOutput? output)>? task = null;
			GoalToCallInfo? goalToCall = null;
			Dictionary<string, object?>? parameters = new();

			if (result && goalToCallOnTrue != null)
			{
				if (VariableHelper.IsVariable(goalToCallOnTrue))
				{
					goalToCallOnTrue.Name = variableHelper.LoadVariables(goalToCallOnTrue.Name)?.ToString();
				}
				goalToCall = goalToCallOnTrue;
			}
			else if (!result && goalToCallOnFalse != null)
			{
				if (VariableHelper.IsVariable(goalToCallOnFalse))
				{
					goalToCallOnFalse.Name = variableHelper.LoadVariables(goalToCallOnFalse.Name)?.ToString();
				}
				goalToCall = goalToCallOnFalse;
			}

			if (goalToCall != null)
			{
				goalToCall.Parameters = variableHelper.LoadVariables(goalToCall.Parameters);

				task = pseudoRuntime.RunGoal(engine, context, goal.RelativeAppStartupFolderPath, goalToCall, goal);
				if (task != null)
				{
					try
					{
						var taskExecuted = await task;
						if (taskExecuted.error != null && taskExecuted.error is not Return)
						{
							if (taskExecuted.error is EndGoal eg)
							{
								if (--eg.Levels > 0) return taskExecuted.error;
							}
							else
							{
								return taskExecuted.error;
							}
						}
						else if (taskExecuted.error is Return r)
						{
							foreach (var variable in r.ReturnVariables)
							{
								memoryStack.Put(variable);
							}
						}
					}
					catch { }

					if (task.IsFaulted)
					{
						return new ExceptionError(task.Exception, $"Error running {goalToCall} - {task.Exception.Message}", goal, goalStep, Key: task.Exception.GetType().FullName ?? "UnhandledError");
					}
				}
			}

			if (result && throwErrorOnTrue != null)
			{
				var module = GetProgramModule<ThrowErrorModule.Program>();
				return await module.Throw(throwErrorOnTrue.errorMessage ?? "Is empty", throwErrorOnTrue.type, throwErrorOnTrue.statusCode);
			}
			if (!result && throwErrorOnFalse != null)
			{
				var module = GetProgramModule<ThrowErrorModule.Program>();
				return await module.Throw(throwErrorOnFalse.errorMessage ?? "Is not empty", throwErrorOnFalse.type, throwErrorOnFalse.statusCode);
			}

			var nextStep = goalStep.NextStep;
			if (nextStep != null)
			{
				bool isIndent = (goalStep.Indent + 4 == nextStep.Indent);

				while (isIndent)
				{
					nextStep.Execute = result && (goalStep.Indent + 4 == nextStep.Indent);

					nextStep = nextStep.NextStep;
					if (nextStep == null) break;
					isIndent = (goalStep.Indent < nextStep.Indent);
				}
			}

			return null;
		}
	}
}

