using NBitcoin.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Errors.Builder;
using PLang.Errors.Events;
using PLang.Errors.Interfaces;
using PLang.Errors.Runtime;
using PLang.Models;
using PLang.Models.Formats;
using PLang.Runtime;
using PLang.Utils;
using static PLang.Modules.BaseBuilder;

namespace PLang.Errors
{
	public class ErrorHelper
	{
		public static async Task OutputError(IEngine engine, IError error, Goal? parentGoal = null)
		{
			try
			{
				if (engine?.Context == null)
				{
					// Fallback when engine context is not available
					Console.WriteLine(error.ToFormat("text"));
					return;
				}

				// Set error in context as %!error% (reserved variable)
				engine.Context.AddVariable(error, variableName: ReservedKeywords.Error);

				var goalInfo = new GoalToCallInfo("/errors/Error");

				await engine.RunGoal(goalInfo, parentGoal ?? Goal.EndOfApp, engine.Context);
			}
			catch (Exception ex)
			{
				// Last resort fallback - simple console output with full exception details
				Console.WriteLine($"\n=== Error (fallback output - Error.goal failed) ===");
				Console.WriteLine($"Error: {error.Message}");
				Console.WriteLine($"  Key: {error.Key}, StatusCode: {error.StatusCode}");
				if (error.Step != null)
				{
					Console.WriteLine($"  At: {error.Step.RelativeGoalPath}:{error.Step.LineNumber}");
				}
				if (error.FixSuggestion != null)
				{
					Console.WriteLine($"  Fix: {error.FixSuggestion}");
				}

				// Print exception chain with stack traces
				Console.WriteLine($"\n--- Exception while running Error.goal ---");
				var currentEx = ex;
				while (currentEx != null)
				{
					Console.WriteLine($"Exception: {currentEx.GetType().Name}: {currentEx.Message}");
					Console.WriteLine($"StackTrace: {currentEx.StackTrace}");
					currentEx = currentEx.InnerException;
					if (currentEx != null)
					{
						Console.WriteLine($"\n--- Inner Exception ---");
					}
				}
			}
		}

		public static string FormatLine(string? txt, string? lineStarter = null, bool indent = false)
		{
			if (txt == null) return null;
			var lines = txt.Trim().Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
			if (lines.Length == 0) return txt;
			var text = String.Empty;
			string tab = (indent) ? "\t" : "";
			for (int i = 0; i < lines.Length; i++)
			{
				if (lineStarter != null)
				{
					text += $"{tab}{lineStarter} {lines[i].TrimStart(lineStarter[0]).Trim()}{Environment.NewLine}";
				}
				else
				{
					text += $"{tab}{lines[i]}{Environment.NewLine}";
				}
			}
			return text.TrimEnd();
		}
		public static object ToFormat(string contentType, IError error, string[]? propertyOrder = null, string? extraInfo = null)
		{
			AppContext.TryGetSwitch(ReservedKeywords.DetailedError, out bool detailedError);
			detailedError = true;

			if (error.ErrorChain.Count > 0)
			{
				for (int i = 0; i < error.ErrorChain.Count; i++)
				{
					if (error.ErrorChain[i].Id == error.Id)
					{
						error = error.ErrorChain[i];
						i = error.ErrorChain.Count;
					}
				}
			}

			var errorType = error.GetType();
			var properties = error.GetType().GetProperties();
			var propertyOrderValue = new Dictionary<string, object?>();

			Goal? goal = null;
			GoalStep? step = null;
			IGenericFunction? genericFunction = null;
			Instruction? instruction = null;
			Dictionary<string, object?>? parameterValues = null;
			Exception? exception = null;
			if (propertyOrder != null)
			{
				foreach (var order in propertyOrder)
				{
					var prop = properties.FirstOrDefault(p => p.Name.Equals(order, StringComparison.OrdinalIgnoreCase));
					if (prop != null)
					{
						propertyOrderValue.Add(prop.Name, prop.GetValue(error));
					}
				}
			}


			var property = properties.FirstOrDefault(p => p.Name.Equals("Goal"));
			if (property != null) goal = (Goal?)property.GetValue(error);

			property = properties.FirstOrDefault(p => p.Name.Equals("Step"));
			if (property != null) step = (GoalStep?)property.GetValue(error);


			property = properties.FirstOrDefault(p => p.Name.Equals("Instruction"));
			if (detailedError && property != null) instruction = (Instruction?)property.GetValue(error);

			property = properties.FirstOrDefault(p => p.Name.Equals("GenericFunction"));
			if (detailedError && property != null) genericFunction = (IGenericFunction?)property.GetValue(error);

			if (genericFunction == null && instruction != null)
			{
				genericFunction = instruction.Function as IGenericFunction;
			}


			property = properties.FirstOrDefault(p => p.Name.Equals("ParameterValues"));
			if (detailedError && property != null) parameterValues = (Dictionary<string, object?>?)property.GetValue(error);

			property = properties.FirstOrDefault(p => p.Name.Equals("Exception"));
			if (detailedError && property != null) exception = (Exception?)property.GetValue(error);
			string? errorSource = null;
			string? fixSuggestions = null;
			if (error.FixSuggestion != null)
			{
				fixSuggestions = $@"Fix Suggestions:
{FormatLine(error.FixSuggestion, null, true)}";
			}
			string? helpfulLinks = null;
			if (error.HelpfulLinks != null)
			{
				helpfulLinks += $@"Helpful Links:
{FormatLine(error.HelpfulLinks, null, true)}";
			}

			string firstLine = "";
			if (error.ErrorChain.Count > 0)
			{
				firstLine += @$"
=== NOTICE! {(error.ErrorChain.Count + 1)} errors happend ===

First we will give you the original error, then each error that occured will show

=== NOTICE! {(error.ErrorChain.Count + 1)} errors happend ===


";
			}
			string eventInfo = null;
			if (error is IEventError ree)
			{
				eventInfo = $"\nError on event:\n\t [{ree.EventBinding.EventType}][{ree.EventBinding.EventScope}][{ree.EventBinding.GoalToBindTo}] - {ree.EventBinding.GoalStep.Text}\n\n";
			}

			firstLine += $@"
================== {error.Key}({error.StatusCode}) ==================
";

			if (step != null)
			{
				firstLine += $@"File: {step.RelativeGoalPath}:{step.LineNumber}
Line: {step.LineNumber}
Key:  {error.Key}
StatusCode:  {error.StatusCode}
Time: {error.CreatedUtc}

================== Error Details ==================
{eventInfo}
Code snippet that the error occured:
	- {step.Text.Replace("\r", "").Replace("\t", "").Replace("\n", "\n\t\t").MaxLength(160)}
		at {step.RelativeGoalPath}:{step.LineNumber}
";
				if (!string.IsNullOrWhiteSpace(step.ModuleType))
				{
					errorSource = $@"Error Source:
	- The error occurred in the module: `{step.ModuleType}`";
					if (genericFunction != null)
					{
						errorSource += $".`{genericFunction.Name}`";
					}
				}

			}
			else if (goal != null)
			{
				firstLine = $@"File: {goal.RelativeGoalPath}
Key:  {error.Key}
StatusCode:  {error.StatusCode}
Time: {error.CreatedUtc}

================== Error Details ==================
{eventInfo}";
			}

			string? variables = null;
			if (error.Variables.Count > 0)
			{
				variables = @"Variables in step:";
				foreach (var variable in error.Variables)
				{
					string value;
					if (variable.PathAsVariable.StartsWith(" %Settings."))
					{
						value = "*****";
					}
					else
					{
						value = JsonHelper.ToStringIgnoreError(variable.Value).MaxLength(150).ReplaceLineEndings("").Trim();
					}
					variables += $"\n\t - {variable.PathAsVariable} => {value}";

					// Show parent object when value is null/empty and parent exists
					if (string.IsNullOrWhiteSpace(value) && variable.Parent != null)
					{
						var parentValue = JsonHelper.ToStringIgnoreError(variable.Parent.Value).MaxLength(300).ReplaceLineEndings(" ").Trim();
						variables += $"\n\t   Parent {variable.Parent.PathAsVariable} => {parentValue}";

						var overParent = variable.Parent.Parent;
						if (overParent != null)
						{
							parentValue = JsonHelper.ToStringIgnoreError(overParent.Value).MaxLength(300).ReplaceLineEndings(" ").Trim();
							variables += $"\n\t     Parent {overParent.PathAsVariable} => {parentValue}";
						}
					}
				}
			}
			string? callStack = null;
			if (goal?.ParentGoal != null)
			{
				var parentGoal = goal;
				int counter = 0;
				while (parentGoal != null)
				{
					if (callStack != null) callStack += "\n\t";

					var currentStep = parentGoal.GoalSteps[parentGoal.CurrentStepIndex];

					callStack += $"{parentGoal.GoalName} - {parentGoal.RelativeGoalPath}:{currentStep.LineNumber}";
					parentGoal = parentGoal.ParentGoal;

					if (counter++ > 100)
					{
						Console.WriteLine($"To deep: ErrorHelper - goalName: {parentGoal?.GoalName}");
						break;
					}
				}
				callStack = "\nCall stack:\n\t" + callStack;
			}

			if (genericFunction != null)
			{
				string paramsStr = $"";
				if (parameterValues == null)
				{
					paramsStr = JsonHelper.ToStringIgnoreError(genericFunction.Parameters).MaxLength(5000);
				}
				else
				{
					paramsStr = GetFormattedGfParameters(genericFunction.Parameters, parameterValues);

				}
				string returnStr = "";
				if (genericFunction.ReturnValues != null && genericFunction.ReturnValues.Count > 0)
				{
					returnStr = "\nThe results will be written into ";
					foreach (var returnValue in genericFunction.ReturnValues)
					{
						returnStr += $"\t- {returnValue.VariableName}\n";
					}
				}
				string? paramInfo = null;
				if (!string.IsNullOrEmpty(paramsStr) || !string.IsNullOrEmpty(returnStr))
				{
					paramInfo = $@"
	- Parameters:
		{FormatLine(paramsStr, "-", true)}
		{FormatLine(returnStr, "-", true)}";
				}

				if (step != null && !string.IsNullOrEmpty(step.ModuleType))
				{
					errorSource = $@"
Error Source:
	- The error occurred in the module: `{step.ModuleType}.{genericFunction.Name}`
	{paramInfo}
".TrimEnd();
				}
			}

			string reasonAndFix = @$"
	{FormatLine(error.Message)}

{fixSuggestions}

{helpfulLinks}
".TrimEnd();

			string message = $@"


{firstLine.TrimEnd()}

Reason: {reasonAndFix}

{variables}

{callStack}

{errorSource}

{FormatLine(extraInfo)}
".TrimEnd();

			if (error.Key == "PaymentRequired")
			{
				message = $@"
======== {error.Key} ========
{reasonAndFix.Trim()}";
			}

			if (contentType == "json")
			{
				var obj = new JObject();
				obj.Add("Message", message);
				if (genericFunction != null)
				{
					obj.Add("Parameters", JsonHelper.ToStringIgnoreError(genericFunction.Parameters));
					obj.Add("ParameterValues", JsonHelper.ToStringIgnoreError(parameterValues));
					obj.Add("ReturnValues", JsonHelper.ToStringIgnoreError(genericFunction.ReturnValues));
				}

				if (callStack != null)
				{
					obj.Add("CallStack", callStack);
				}
				if (exception != null)
				{
					var tmp = exception;

					string exceptionString = "";
					while (tmp != null)
					{
						exceptionString += tmp.ToString() + "\n";
						tmp = tmp.InnerException;
					}

					obj.Add("Exception", exceptionString);

				}
				if (detailedError)
				{
					obj.Add("Error", JObject.FromObject(error));
				}
				return obj;
			}

			if (exception != null)
			{

				var tmp = exception;

				string exceptionString = "";
				while (tmp != null)
				{
					exceptionString += tmp.ToString() + "\n";
					tmp = tmp.InnerException;
				}

				message += $@"

For C# Developers:
	- {FormatLine(exception.Message)}

	StackTrace: {FormatLine(exceptionString)}
";
			}
			if (error.ErrorChain.Count > 0)
			{
				foreach (var nextError in error.ErrorChain)
				{
					if (nextError == error) continue;

					message += "\n\n\t\t<==== Next error ===> ";
					message += nextError.ToFormat();

				}
			}
			return message;

		}

		private static string GetFormattedGfParameters(List<Parameter>? parameters, Dictionary<string, object?> parameterValues)
		{
			string paramsStr = "\n";
			foreach (var param in parameters)
			{
				var keyValue = parameterValues.FirstOrDefault(p => p.Key.Equals(param.Name, StringComparison.OrdinalIgnoreCase));
				if (keyValue.Value != null)
				{
					var paramValue = keyValue.Value;
					if (paramValue != null && TypeHelper.IsConsideredPrimitive(paramValue.GetType()))
					{
						var value = paramValue?.ToString().MaxLength(150).ReplaceLineEndings("").Trim() ?? "[empty]";
						paramsStr += $"\t\t{param.Name} : {value}\n";
					}
					else
					{
						var value = JsonHelper.ToStringIgnoreError(paramValue).MaxLength(150).ReplaceLineEndings("").Trim() ?? "[empty]";
						paramsStr += $"\t\t{param.Name} : {value}\n";
					}

				}
				else
				{
					paramsStr += $"\t\t{param.Name} : [empty]\n";
				}
			}

			return paramsStr;
		}

		internal static string MakeForLlm(IBuilderError error)
		{
			string errorMessage = $@"Previous LLM request caused following <error>, try to fix it.
<error>";

			List<string> repeatMessage = new();
			if (!string.IsNullOrEmpty(error.Message))
			{
				repeatMessage.Add(error.Message);
				errorMessage += $"\nError Message: {error.Message}\n";
			}
			if (!string.IsNullOrEmpty(error.FixSuggestion))
			{
				errorMessage += "\n\t- FixSuggestion:" + error.FixSuggestion;
			}
			string? functionJson = null;
			if (error.ErrorChain.Count > 0)
			{
				foreach (var errorChain in error.ErrorChain)
				{

					if (repeatMessage.Contains(errorChain.Message))
					{
						errorMessage += @$"ATTENTION: This is the {repeatMessage.Count + 1} time you have made this error. Make SURE YOU FIX IT:
Error Message: " + errorChain.Message;
						if (error.Step != null) error.Step.Retry = false;
					}
					else
					{
						errorMessage += "\nError Message: " + errorChain.Message;
					}
					repeatMessage.Add(error.Message);
					if (!string.IsNullOrWhiteSpace(errorChain.FixSuggestion)) errorMessage += "\n\t- FixSuggestion: " + errorChain.FixSuggestion;
					if (errorChain.Step?.Instruction?.FunctionJson != null)
					{
						functionJson = errorChain.Step?.Instruction?.FunctionJson.ToString();
					}
				}
			}

			errorMessage += "\n</error>";
			if (functionJson != null)
			{

				errorMessage += @$"

This is the previous LLM response:
""Name"": is the name of the method selected

```json
{functionJson}
```
";
			}

			return errorMessage;
		}



		public static string GetErrorMessageFromChain(IError errorWithChain)
		{
			string message = "";
			List<IError> errors = new();
			if (errorWithChain is GroupedErrors ge)
			{
				errors = ge.ErrorChain;
				foreach (var error in errors)
				{
					message += $"\t- {error.Message}\n";
				}

				if (ge.Step != null)
				{
					message += $"\t\t - at {ge.Step.RelativeGoalPath}:{ge.Step.LineNumber}";
				}
			}
			else if (errorWithChain.ErrorChain.Count > 0)
			{
				// Handle base Error with ErrorChain
				errors = errorWithChain.ErrorChain;
				if (errorWithChain.Step != null)
				{
					message += $"\t- {errorWithChain.Message}\n\t\t - at {errorWithChain.Step.RelativeGoalPath}:{errorWithChain.Step.LineNumber}\n";
				}
				else
				{
					message += $"\t- {errorWithChain.Message}\n";
				}


				foreach (var error in errors)
				{
					if (error.Step != null)
					{
						message += $"\t- {error.Message}\n\t\t - at {error.Step.RelativeGoalPath}:{error.Step.LineNumber}\n";
					}
					else
					{
						message += $"\t- {error.Message}";
					}
				}
			}
			return message;
		}
	}



}
