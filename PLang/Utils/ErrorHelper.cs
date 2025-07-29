using NBitcoin.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Errors.Events;
using PLang.Errors.Interfaces;
using PLang.Errors.Runtime;
using PLang.Models.Formats;
using static PLang.Modules.BaseBuilder;

namespace PLang.Utils
{
	public class ErrorHelper
	{
		public static IBuilderError GetMultipleBuildError(IBuilderError initialError, IError? secondError)
		{
			if (secondError == null || initialError == secondError) return initialError;

			var multipleError = new MultipleBuildError(initialError);
			multipleError.Add(secondError);
			return multipleError;
		}
		public static IError GetMultipleError(IError initialError, IError? secondError)
		{
			if (secondError == null) return initialError;

			var multipleError = new MultipleError(initialError);
			multipleError.Add(secondError);
			return multipleError;
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

			if (error is RuntimeEventError rve && rve.InitialError != null)
			{
				//error = rve.InitialError;
			}


			if (error is IUserInputError && contentType == "json")
			{
				JsonRpcError jsonRpcError = new()
				{
					Code = error.StatusCode,
					Message = error.Message//, Data = data
				};

				if (JsonHelper.IsJson(error.Message))
				{
					return error.Message;
				}
				else
				{
					var obj = new JObject();
					obj.Add("error", true);
					obj.Add("message", error.Message);
					return obj;
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
				fixSuggestions = $@"🛠️  Fix Suggestions:
{FormatLine(error.FixSuggestion, null, true)}";
			}
			string? helpfulLinks = null;
			if (error.HelpfulLinks != null)
			{
				helpfulLinks += $@"🔗 Helpful Links:
{FormatLine(error.HelpfulLinks, null, true)}";
			}

			string firstLine = $"";
			if (step != null)
			{


				firstLine = $@"📄 File: {step.RelativeGoalPath}:{step.LineNumber}
🔢 Line: {step.LineNumber}
🧩 Key:  {error.Key}
#️⃣ StatusCode:  {error.StatusCode}

🔍   ================== Error Details ==================   🔍

📜 Code snippet that the error occured:
	- {step.Text.Replace("\r", "").Replace("\t", "").Replace("\n", "\n\t\t").MaxLength(160)}
		at {step.RelativeGoalPath}:{step.LineNumber}
";
				if (!string.IsNullOrWhiteSpace(step.ModuleType))
				{
					errorSource = $@"📦 Error Source:
	- The error occurred in the module: `{step.ModuleType}`";
					if (genericFunction != null)
					{
						errorSource += $".`{genericFunction.Name}`";
					}
				}

			}
			else if (goal != null)
			{
				firstLine = $@"📄 File: {goal.RelativeGoalPath}";
			}

			string? variables = null;
			if (error.Variables.Count > 0)
			{
				variables = @"🏷️  Variables:";
				foreach (var variable in error.Variables)
				{
					string value;
					if (variable.PathAsVariable.StartsWith("%Settings."))
					{
						value = "*****";
					}
					else
					{
						value = JsonConvert.SerializeObject(variable.Value).MaxLength(5000);
					}
					variables += $"\n\t - {variable.PathAsVariable} => {value}";
				}
			}
			string? callStack = null;
			if (goal?.ParentGoal != null)
			{
				var parentGoal = goal;
				while (parentGoal != null)
				{
					if (callStack != null) callStack += "\n\t";

					var currentStep = parentGoal.GoalSteps[parentGoal.CurrentStepIndex];

					callStack += $"{parentGoal.GoalName} - {parentGoal.RelativeGoalPath}:{currentStep.LineNumber}";
					parentGoal = parentGoal.ParentGoal;
				}
				callStack = "\n🛝  Call stack:\n\t" + callStack;
			}

			if (genericFunction != null)
			{
				string paramsStr = $"";
				if (parameterValues == null)
				{
					paramsStr = JsonConvert.SerializeObject(genericFunction.Parameters).MaxLength(5000);
				}
				else
				{
					foreach (var param in genericFunction.Parameters)
					{
						if (parameterValues.ContainsKey(param.Name))
						{
							var value = parameterValues[param.Name]?.ToString().MaxLength(5000) ?? "[empty]";
							paramsStr += $"\t{param.Name} : {value}\n";
						}
						else
						{
							paramsStr += $"\t{param.Name} : [empty]\n";
						}
					}
				}
				string returnStr = "";
				if (genericFunction.ReturnValues != null && genericFunction.ReturnValues.Count > 0)
				{
					returnStr = "\nThe results will be written into ";
					foreach (var returnValue in genericFunction.ReturnValues)
					{
						returnStr += $"\t- %{returnValue.VariableName}%\n";
					}
				}
				string? paramInfo = null;
				if (!string.IsNullOrEmpty(paramsStr) || !string.IsNullOrEmpty(returnStr))
				{
					paramInfo = $@"- Parameters:
		{FormatLine(paramsStr, "-", true)}
		{FormatLine(returnStr, "-", true)}";
				}

				if (step != null && !string.IsNullOrEmpty(step.ModuleType))
				{
					errorSource = $@"
📦 Error Source:
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
🔴   ================== {error.Key}({error.StatusCode}) ==================   🔴

{firstLine.TrimEnd()}

🧐 Reason: {reasonAndFix}

{variables}

{callStack}

{errorSource}

{FormatLine(extraInfo)}
".TrimEnd();

			if (error.Key == "PaymentRequired")
			{
				message = $@"
🔴 ======== {error.Key} ========
{reasonAndFix.Trim()}";
			}

			if (contentType == "json")
			{
				var obj = new JObject();
				obj.Add("Message", message);
				if (genericFunction != null)
				{
					obj.Add("Parameters", JsonConvert.SerializeObject(genericFunction.Parameters));
					obj.Add("ParameterValues", JsonConvert.SerializeObject(parameterValues));
					obj.Add("ReturnValues", JsonConvert.SerializeObject(genericFunction.ReturnValues));
				}

				if (callStack != null)
				{
					obj.Add("CallStack", callStack);
				}
				if (exception != null)
				{
					obj.Add("Exception", exception.ToString());
				}
				if (detailedError)
				{
					obj.Add("Error", JObject.FromObject(error));
				}
				return obj;
			}

			if (exception != null)
			{
				message += $@"

👨‍💻 For C# Developers:
	- {FormatLine(exception.Message)}

	StackTrace: {FormatLine(exception.StackTrace)}
";
			}

			return message;
		}

		internal static string MakeForLlm(IBuilderError error)
		{
			string errorMessage = $@"Previous LLM request caused following <error>, try to fix it.
<error>";

			List<string> repeatMessage = new();
			if (!string.IsNullOrEmpty(error.Message)) {
				repeatMessage.Add(error.Message);
				errorMessage += $"\nError Message: { error.Message}\n";
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
						errorMessage += @$"ATTENTION: This is the {repeatMessage.Count+1} time you have made this error. Make SURE YOU FIX IT:
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
			else if (errorWithChain is MultipleError me)
			{
				errors = me.ErrorChain;
				if (me.InitialError.Step != null)
				{
					message += $"\t- {me.InitialError.Message}\n\t\t - at {me.InitialError.Step.RelativeGoalPath}:{me.InitialError.Step.LineNumber}\n";
				}
				else
				{
					message += $"\t- {me.InitialError.Message}\n";
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
