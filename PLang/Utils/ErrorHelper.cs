using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium.DevTools.V124.Page;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Errors.Runtime;
using static PLang.Modules.BaseBuilder;

namespace PLang.Utils
{
	public class ErrorHelper
	{
		public static IBuilderError GetMultipleBuildError(IBuilderError initialError, IError? secondError)
		{
			if (secondError == null || initialError == secondError) return initialError;

			var multipleError = new MultipleBuildError();
			multipleError.Add(initialError);
			multipleError.Add(secondError);
			return multipleError;
		}
		public static IError GetMultipleError(IError initialError, IError? secondError)
		{
			if (secondError == null) return initialError;

			var multipleError = new MultipleError();
			multipleError.Add(initialError);
			multipleError.Add(secondError);
			return multipleError;
		}

		public static string FormatLine(string txt)
		{
			var lines = txt.Trim().Split(Environment.NewLine);
			if (lines.Length == 0) return txt;
			var text = lines[0];
			for (int i = 1; i < lines.Length; i++)
			{
				text += Environment.NewLine + "\t  " + lines[i].Trim();
			}
			return text;
		}
		public static object ToFormat(string contentType, IError error, string[]? propertyOrder = null, string? extraInfo = null)
		{
			AppContext.TryGetSwitch(ReservedKeywords.DetailedError, out bool detailedError);
			if (error is UserDefinedError && contentType == "json")
			{
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
			GenericFunction? genericFunction = null;
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

			property = properties.FirstOrDefault(p => p.Name.Equals("GenericFunction"));
			if (detailedError && property != null) genericFunction = (GenericFunction?)property.GetValue(error);

			property = properties.FirstOrDefault(p => p.Name.Equals("ParameterValues"));
			if (detailedError && property != null) parameterValues = (Dictionary<string, object?>?)property.GetValue(error);

			property = properties.FirstOrDefault(p => p.Name.Equals("Exception"));
			if (detailedError && property != null) exception = (Exception?)property.GetValue(error);
			string? errorSource = null;
			string? fixSuggestions = null;
			if (error.FixSuggestion != null)
			{
				fixSuggestions = $@"🛠️ Fix Suggestions:
	- {FormatLine(error.FixSuggestion)}";
			}
			string? helpfulLinks = null;
			if (error.HelpfulLinks != null)
			{
				helpfulLinks += $@"🔗 Helpful Links:
	- {FormatLine(error.HelpfulLinks)}";
			}

			string firstLine = $"";
			if (step != null)
			{
				firstLine = $@"📄 File: {step.Goal.RelativeGoalPath}:{step.LineNumber}
🔢 Line: {step.LineNumber}

🔎 Error Details - Code snippet that the error occured:
	`- {FormatLine(step.Text.MaxLength(160))}`
";
				errorSource = $@"📦 Error Source:
	- The error occurred in the module: `{step.ModuleType}`";

			}
			else if (goal != null)
			{
				firstLine = $@"📄 File: {goal.RelativeGoalPath}";
			}


			if (genericFunction != null)
			{
				string paramsStr = $"";
				if (parameterValues == null)
				{
					paramsStr = JsonConvert.SerializeObject(genericFunction.Parameters);
				}
				else
				{
					foreach (var param in genericFunction.Parameters)
					{
						if (parameterValues.ContainsKey(param.Name))
						{
							paramsStr += $"\t{param.Name} : {parameterValues[param.Name] ?? "[empty]"}\n";
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
		{FormatLine(paramsStr)}
		{FormatLine(returnStr)}";
				}

				if (step != null)
				{
					errorSource = $@"
📦 Error Source:
	- The error occurred in the module: `{step.ModuleType}.{genericFunction.FunctionName}`
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
🔴 ======== {error.Key} ========
{firstLine.TrimEnd()}

🚫 Reason: {reasonAndFix}

{errorSource}

{extraInfo}
".TrimEnd();

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

👨‍💻 For Developers:
	- {FormatLine(exception.ToString())}";
			}

			return message;
		}


	}
}
