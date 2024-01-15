using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Attributes;
using PLang.Building.Model;
using PLang.Exceptions;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using System.Reflection;
using System.Runtime.Caching;
using static PLang.Modules.BaseBuilder;

namespace PLang.Utils
{
	public class MethodHelper
	{
		private GoalStep? goalStep;
		private readonly VariableHelper variableHelper;
		private readonly ITypeHelper typeHelper;
		private readonly ILlmService llmService;

		public MethodHelper(GoalStep? goalStep, VariableHelper variableHelper, ITypeHelper typeHelper, ILlmService llmService)
		{
			this.goalStep = goalStep;
			this.variableHelper = variableHelper;
			this.typeHelper = typeHelper;
			this.llmService = llmService;
		}

		public async Task<(MethodInfo method, Dictionary<string, object> parameterValues)> GetMethodAndParameters(object callingInstance, GenericFunction function)
		{
			string cacheKey = callingInstance.GetType().FullName + "_" + function.FunctionName;
			MethodInfo? method = null;
			if ((AppContext.TryGetSwitch(ReservedKeywords.Debug, out bool isDebug) && isDebug) || !MethodCache.Cache.TryGetValue(cacheKey, out method))
			{
				var methods = callingInstance.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);
				method = methods.FirstOrDefault(p => p.Name == function.FunctionName && IsParameterMatch(p, function.Parameters) == null);
				if (method == null)
				{
					await HandleMethodNotFound(callingInstance, function);
				}
				if (!isDebug)
				{
					MethodCache.Cache.AddOrReplace(cacheKey, method);
				}
			}

			Dictionary<string, object?> parameterValues = GetParameterValues(method, function);
			return (method, parameterValues);
		}

		private async Task HandleMethodNotFound(object callingInstance, GenericFunction function)
		{
			var methods = typeHelper.GetMethodsAsString(callingInstance.GetType(), function.FunctionName);
			string system = @"Try to map user statement to methods that are available in my class, 
variables are defined with starting and ending %

Answer in same natural language as user statement. Do not use the method name directly in you answer.
Your answer cannot be same as user statement.

example of answer:
{text:""read file.txt, write into %content%""}
{text:""add %item% to list, write to %list%""}

you must answer in JSON, scheme:
{text:string}";
			string assistant = @$"## methods available ##
{methods}
## methods available ##";
			string user = goalStep.Text;
			var llmQuestion = new LlmQuestion("HandleMethodNotFound", system, user, assistant);

			var response = await llmService.Query<MethodNotFoundResponse>(llmQuestion);
			throw new MissingMethodException($"Method {function.FunctionName} could not be found that matches with your statement. Example of command could be: {response.Text}");
		}
		public record MethodNotFoundResponse(string Text);

		public string? IsParameterMatch(MethodInfo p, List<Parameter> parameters)
		{
			foreach (var methodParameter in p.GetParameters())
			{
				var methodType = methodParameter.ParameterType.Name.ToLower();
				if (methodType.Contains("`")) methodType = methodType.Substring(0, methodType.IndexOf("`"));
				if (methodType == "nullable" && methodParameter.ParameterType.GenericTypeArguments.Length > 0)
				{
					methodType = methodParameter.ParameterType.GenericTypeArguments[0].Name.ToLower();
				}

				if (parameters.FirstOrDefault(p => p.Type.ToLower().StartsWith(methodType)) == null && parameters.FirstOrDefault(p => p.Type.ToLower() == methodParameter.ParameterType.FullName.ToLower()) == null)
				{
					if (!methodParameter.ParameterType.Name.ToLower().StartsWith("nullable") && !methodParameter.IsOptional && !methodParameter.HasDefaultValue)
					{
						return $"{methodParameter.Name} ({methodParameter.ParameterType.Name})";
					}

				}
			}
			return null;
		}

		public Dictionary<string, object?> GetParameterValues(MethodInfo method, GenericFunction function)
		{
			var parameterValues = new Dictionary<string, object?>();
			var parameters = method.GetParameters();
			if (parameters.Length == 0) return parameterValues;


			foreach (var parameter in parameters)
			{
				if (parameter.Name == null) continue;

				var inputParameter = function.Parameters.FirstOrDefault(p => p.Name == parameter.Name);
				if (inputParameter == null && !parameter.IsOptional && !parameter.ParameterType.Name.StartsWith("Nullable"))
				{
					throw new ParameterException($"Could not find parameter {parameter.Name}", goalStep);
				}

				var variableValue = inputParameter?.Value;
				try
				{
					if (variableValue == null || string.IsNullOrEmpty(variableValue.ToString()))
					{
						SetEmptyParameter(parameterValues, parameter, variableValue);
						continue;
					}

					var handlesAttribute = parameter.CustomAttributes.FirstOrDefault(p => p.AttributeType == typeof(HandlesVariableAttribute));

					if (parameter.ParameterType.Name.StartsWith("Dictionary"))
					{
						SetDictionaryParameter(parameter, variableValue, handlesAttribute, parameterValues);
					}
					else if (parameter.ParameterType.Name.StartsWith("List"))
					{
						SetListParameter(parameter, variableValue, handlesAttribute, parameterValues);
						
					}
					else if (parameter.ParameterType.IsArray)
					{
						SetArrayParameter(parameter, variableValue, handlesAttribute, parameterValues);						
					}
					else
					{
						SetObjectParameter(parameter, variableValue, handlesAttribute, parameterValues);						
					}


				}
				catch (PropertyNotFoundException pe) { throw; }
				catch (Exception ex)
				{
					if (ex is AskUserException) throw;

					throw new ParameterException($"Cannot convert {inputParameter?.Value} on parameter {parameter.Name} - value:{variableValue}", goalStep, ex);
				}

			}
			return parameterValues;
		}

		private void SetObjectParameter(ParameterInfo parameter, object variableValue, CustomAttributeData? handlesAttribute, Dictionary<string, object?> parameterValues)
		{
			object value = variableValue;
			if (handlesAttribute == null)
			{
				value = variableHelper.LoadVariables(variableValue);
			}

			if (parameter.ParameterType != typeof(string) && parameter.ParameterType == typeof(object) && value != null && JsonHelper.IsJson(value.ToString()))
			{
				value = JsonConvert.DeserializeObject(value.ToString(), parameter.ParameterType);
			}
			else
			{
				value = ConvertToType(value, parameter);
			}
			parameterValues.Add(parameter.Name, value);
		}

		private void SetArrayParameter(ParameterInfo parameter, object variableValue, CustomAttributeData? handlesAttribute, Dictionary<string, object?> parameterValues)
		{
			bool variableValueIsArray = variableValue.ToString().StartsWith("[");
			int arrayLength = variableValueIsArray ? ((JArray)variableValue).Count : 1;
			var elementType = parameter.ParameterType.GetElementType();
			Array newArray = Array.CreateInstance(elementType, arrayLength);
			for (int i = 0; i < arrayLength; i++)
			{
				var tmp = (variableValueIsArray) ? ((JArray)variableValue)[i] : variableValue;

				if (handlesAttribute == null)
				{
					newArray.SetValue(Convert.ChangeType(variableHelper.LoadVariables(tmp), elementType), i);
				}
				else
				{
					newArray.SetValue(Convert.ChangeType(tmp, elementType), i);
				}
			}

			parameterValues.Add(parameter.Name, newArray);
		}

		private void SetListParameter(ParameterInfo parameter, object variableValue, CustomAttributeData? handlesAttribute, Dictionary<string, object?> parameterValues)
		{
			System.Collections.IList list;
			if (variableHelper.IsVariable(variableValue))
			{
				list = variableHelper.LoadVariables(variableValue) as System.Collections.IList;
			}
			else if (variableValue is JObject)
			{
				list = JArray.FromObject(variableValue) as System.Collections.IList;
			}
			else
			{
				list = ((JArray)variableValue).ToObject(parameter.ParameterType) as System.Collections.IList;
			}

			if (handlesAttribute != null)
			{
				parameterValues.Add(parameter.Name, list);
				return;
			}

			for (int i = 0; list != null && i < list.Count; i++)
			{
				object obj = variableHelper.LoadVariables(list[i]);
				if (obj != null && parameter.ParameterType.GenericTypeArguments[0] == typeof(string))
				{
					
					list[i] = obj.ToString();
				}
				else
				{
					list[i] = variableHelper.LoadVariables(list[i]);
				}
			}
			parameterValues.Add(parameter.Name, list);
		}

		private void SetDictionaryParameter(ParameterInfo parameter, object variableValue, CustomAttributeData? handlesAttribute, Dictionary<string, object?> parameterValues)
		{
			Dictionary<string, object?>? dict;
			if (variableHelper.IsVariable(variableValue))
			{
				dict = variableHelper.LoadVariables(variableValue) as Dictionary<string, object?>;
			}
			else
			{
				if (variableValue is JArray array)
				{
					dict = array.ToObject<List<JObject>>()
						.ToDictionary(
						jobj => jobj.Properties().First().Value.ToString(),
						jobj => (object?)jobj.Properties().Skip(1).FirstOrDefault()?.Value.ToString()
						);
				}
				else
				{
					dict = ((JObject)variableValue).ToObject<Dictionary<string, object?>>();
				}
			}
			if (dict == null) dict = new();

			if (handlesAttribute != null)
			{
				parameterValues.Add(parameter.Name, dict);
				return;
			}

			foreach (var item in dict)
			{
				dict[item.Key] = variableHelper.LoadVariables(item.Value);
			}
			parameterValues.Add(parameter.Name, dict);
		}

		private static void SetEmptyParameter(Dictionary<string, object?> parameterValues, ParameterInfo parameter, object? variableValue)
		{
			if (variableValue != null && parameter.ParameterType == typeof(string))
			{
				parameterValues.Add(parameter.Name, variableValue);
			}
			else if (parameter.HasDefaultValue)
			{
				parameterValues.Add(parameter.Name, parameter.DefaultValue);
			}
			else
			{
				if (parameter.CustomAttributes.FirstOrDefault(p => p.AttributeType.Name == "NullableAttribute") != null || parameter.ParameterType.Name.StartsWith("Nullable"))
				{
					parameterValues.Add(parameter.Name, null);
				}
				else
				{
					parameterValues.Add(parameter.Name, Type.Missing);
				}
			}
		}

		private object? ConvertToType(object value, ParameterInfo parameterInfo)
		{
			var targetType = parameterInfo.ParameterType;

			if (targetType == null)
				throw new ArgumentNullException(nameof(targetType));

			if (value == null)
				return null;

			// Directly return the value if it's already of the target type
			if (targetType.IsInstanceOfType(value))
				return value;

			try
			{
				if (targetType.Name.StartsWith("Nullable"))
				{
					targetType = targetType.GenericTypeArguments[0];
				}

				var parseMethod = targetType.GetMethod("Parse", new[] { typeof(string) });
				if (parseMethod != null)
				{
					return parseMethod.Invoke(null, new object[] { value.ToString() });
				}

				
			}
			catch { }

			try
			{
				return Convert.ChangeType(value, targetType);
			}
			catch (Exception ex)
			{
				return parameterInfo.DefaultValue;
			}
		}
	}
}
