using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Attributes;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Errors.Handlers;
using PLang.Errors.Methods;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.LlmService;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Caching;
using System.Text.RegularExpressions;
using System.Xml;
using static PLang.Modules.BaseBuilder;

namespace PLang.Utils
{
    public class MethodHelper
	{
		private GoalStep goalStep;
		private readonly VariableHelper variableHelper;
		private readonly MemoryStack memoryStack;
		private readonly ITypeHelper typeHelper;
		private readonly ILlmServiceFactory llmServiceFactory;

		public MethodHelper(GoalStep goalStep, VariableHelper variableHelper, MemoryStack memoryStack, ITypeHelper typeHelper, ILlmServiceFactory llmServiceFactory)
		{
			this.goalStep = goalStep;
			this.variableHelper = variableHelper;
			this.memoryStack = memoryStack;
			this.typeHelper = typeHelper;
			this.llmServiceFactory = llmServiceFactory;
		}

		public async Task<MethodInfo?> GetMethod(object callingInstance, MethodExecution methodExecution)
		{
			string cacheKey = callingInstance.GetType().FullName + "_" + methodExecution.MethodName;

			var methods = callingInstance.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);
			string? error = null;
			var method = methods.FirstOrDefault(p => {
				if (p.Name == methodExecution.MethodName) {
					error = IsParameterMatch(p, methodExecution.Parameters);
					if (error == null) return true;
				}

				return false;
			});
			if (method != null) return method;

			throw new MissingMethodException($"Method {methodExecution.FunctionName} could not be found that matches with your statement. " + error);
			//await HandleMethodNotFound(callingInstance, function);
			return null;
		}


		private async Task HandleMethodNotFound(object callingInstance, Modules.BaseBuilder.MethodExecution function)
		{
			throw new MissingMethodException($"Method {function.FunctionName} could not be found that is defined in your instruction file.");

			var methods = typeHelper.GetMethodsAsString(callingInstance.GetType(), function.FunctionName);
			string system = @"Try to map user statement to methods that are available in my class, 
variables are defined with starting and ending %

Answer in same natural language as user statement. Do not use the method name directly in you answer.
Your answer cannot be same as user statement.

example of answer:
{text:""read file.txt, write into %content%""}
{text:""add %item% to list, write to %list%""}
";
			string assistant = @$"## methods available ##
{methods}
## methods available ##";
			string user = goalStep.Text;

			var promptMessage = new List<LlmMessage>();
			promptMessage.Add(new LlmMessage("system", system));
			promptMessage.Add(new LlmMessage("assistant", assistant));
			promptMessage.Add(new LlmMessage("user", user));

			var llmRequeset = new LlmRequest("HandleMethodNotFound", promptMessage);

			(var response, var queryError) = await llmServiceFactory.CreateHandler().Query<MethodNotFoundResponse>(llmRequeset);
			throw new MissingMethodException($"Method {function.FunctionName} could not be found that matches with your statement. Example of command could be: {response.Text}");
		}
		public record MethodNotFoundResponse(string Text);


		public GroupedBuildErrors? ValidateFunctions(Modules.BaseBuilder.MethodExecution[] functions, string module, MemoryStack memoryStack)
		{
			var multipleError = new GroupedBuildErrors("InvalidFunction");
			if (functions == null || functions[0] == null) return null;

			foreach (var function in functions)
			{

				if (function.FunctionName == null || function.FunctionName.ToUpper() == "N/A")
				{
					multipleError.Add(new InvalidFunctionsError(function.FunctionName ?? "N/A", "", true));
				}
				else
				{
					var runtimeType = typeHelper.GetRuntimeType(module);
					if (runtimeType == null)
					{
						throw new BuilderException($"Could not load {module}.Program");
					}

					var instanceFunctions = runtimeType.GetMethods().Where(p => p.Name == function.FunctionName);
					if (instanceFunctions.Count() == 0)
					{
						multipleError.Add(new InvalidFunctionsError(function.FunctionName, $"Could not find {function.FunctionName} in module", true));
					}
					else
					{

						foreach (var instanceFunction in instanceFunctions)
						{
							var parameterError = IsParameterMatch(instanceFunction, function.Parameters);
							if (parameterError == null)
							{
								if (instanceFunction.ReturnType != typeof(Task) && function.ReturnValues != null && function.ReturnValues.Count > 0)
								{
									foreach (var returnValue in function.ReturnValues)
									{
										memoryStack.PutForBuilder(returnValue.VariableName, returnValue.Type);
									}
								}
							}
							else
							{
								multipleError.Add(new InvalidFunctionsError(function.FunctionName, $"Parameters don't match with {function.FunctionName} - {parameterError}", false));
							}

						}

					}
				}
			}
			return (multipleError.Count > 0) ? multipleError : null;
		}



		public string? IsParameterMatch(MethodInfo p, List<ParameterDescriptionResponse> parameters)
		{
			string? error = null;
			foreach (var methodParameter in p.GetParameters())
			{
				var parameterType = methodParameter.ParameterType.Name.ToLower();
				if (parameterType.Contains("`")) parameterType = parameterType.Substring(0, parameterType.IndexOf("`"));

				var parameter = parameters.FirstOrDefault(x => x.Name == methodParameter.Name);
				if (parameter == null && parameterType != "nullable" && !methodParameter.HasDefaultValue && !methodParameter.IsOptional)
				{
					error += $"{methodParameter.Name} ({parameterType}) is missing from parameters. {methodParameter.Name} is a required parameter\n";
				}
				else if (parameter != null && parameterType == "string" && methodParameter.CustomAttributes.Count() > 0 && methodParameter.CustomAttributes.First().AttributeType.Name == "NullableAttribute" && parameter.Value == null)
				{
					error += $"{methodParameter.Name} ({parameterType}) is missing from parameters. {methodParameter.Name} is a required parameter\n";
				}
				
				if (parameter != null && parameter.Value != null && parameterType == "string" && parameter.Value.ToString().StartsWith("\"") && parameter.Value.ToString().EndsWith("\""))
				{
					error += $"{methodParameter.Name} is string, the property Value cannot start and end with quote(\").";
				}

				if (parameterType == "nullable" && methodParameter.ParameterType.GenericTypeArguments.Length > 0)
				{
					parameterType = methodParameter.ParameterType.GenericTypeArguments[0].Name.ToLower();
				}
				string? parameterTypeName = methodParameter.ParameterType.FullName;
				if (parameterTypeName == null)
				{
					throw new ArgumentNullException($"Parameter does not have type: {methodParameter.ParameterType}");
				}
				if (parameterTypeName == "System.Object")
				{
					continue;
				}

				if (parameters.FirstOrDefault(p => p.Type.ToLower().StartsWith(parameterType)) == null && parameters.FirstOrDefault(p => p.Type.ToLower() == parameterTypeName!.ToLower()) == null)
				{
					// temp thing, should be removed
					if (parameterTypeName == "PLang.Models.GoalToCall")
					{
						parameterTypeName = "String";
						if (parameters.FirstOrDefault(p => p.Type.ToLower().StartsWith(parameterTypeName)) == null && parameters.FirstOrDefault(p => p.Type.ToLower() == parameterTypeName!.ToLower()) == null)
						{
							error += $"{methodParameter.Name} ({methodParameter.ParameterType.Name}) is missing\n";
						}
					} else if (!methodParameter.ParameterType.Name.ToLower().StartsWith("nullable") && !methodParameter.IsOptional && !methodParameter.HasDefaultValue)
					{
						error += $"{methodParameter.Name} ({methodParameter.ParameterType.Name}) is missing\n";
					}

				}
			}
			return error;
		}

		public Dictionary<string, object?> GetParameterValues(MethodInfo method, MethodExecution function)
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
					if (handlesAttribute == null && VariableHelper.IsVariable(variableValue))
					{
						var ov = variableHelper.GetObjectValue(variableValue.ToString(), false);
						if (ov != null && ov.Value != null && parameter.ParameterType.IsInstanceOfType(ov.Value))
						{
							//parameterValues.Add(inputParameter.Name, ov.Value);
							//continue;
						}
						else if (ov != null && ov.Initiated && ov.Value == null)
						{
							parameterValues.Add(inputParameter.Name, ov.Value);
							continue;
						}

					} 

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
				catch (PropertyNotFoundException) { throw; }
				catch (Exception ex)
				{
					if (ex is AskUserError) throw;

					throw new ParameterException($"Cannot convert {inputParameter?.Value} on parameter {parameter.Name} - value:{variableValue}", goalStep, ex);
				}

			}
			return parameterValues;
		}

		private void SetObjectParameter(ParameterInfo parameter, object variableValue, CustomAttributeData? handlesAttribute, Dictionary<string, object?> parameterValues)
		{
			object? value = variableValue;
			if (handlesAttribute == null)
			{
				value = variableHelper.LoadVariables(variableValue);
			}

			if (value != null)
			{

				if (parameter.ParameterType != typeof(string) && parameter.ParameterType == typeof(object) && value != null && JsonHelper.IsJson(value.ToString()))
				{
					value = JsonConvert.DeserializeObject(value.ToString()!, parameter.ParameterType);
				}
				else
				{
					value = ConvertToType(value, parameter);
				}
			}
			parameterValues.Add(parameter.Name!, value);
		}

		private void SetArrayParameter(ParameterInfo parameter, object variableValue, CustomAttributeData? handlesAttribute, Dictionary<string, object?> parameterValues)
		{
			bool variableValueIsArray = variableValue.ToString().StartsWith("[");
			if (variableValue is string)
			{
				variableValue = JArray.Parse(variableValue.ToString());
			}

			int arrayLength = variableValueIsArray ? ((JArray)variableValue).Count : 1;
			var rootElementType = parameter.ParameterType.GetElementType();
			var mainElementType = parameter.ParameterType;
			Type elementType;
			if (mainElementType.IsArray && variableValueIsArray)
			{
				var value = (handlesAttribute != null) ? variableValue: variableHelper.LoadVariables(variableValue);
				if (value is JArray array)
				{
					parameterValues.Add(parameter.Name, array.ToObject(mainElementType));
					return;
				} else if (value is string && JsonHelper.IsJson(value, out object strArray))
				{
					parameterValues.Add(parameter.Name, ((JToken) strArray).ToObject(mainElementType));
					return;
				} else if (value.GetType().IsArray) 
				{
					parameterValues.Add(parameter.Name, value);
					return;
				}
				
			}

			if (!variableValueIsArray)
			{
				parameterValues.Add(parameter.Name, variableHelper.LoadVariables(variableValue));
				return;
			}


			Array newArray = Array.CreateInstance(rootElementType, arrayLength);
			for (int i = 0; i < arrayLength; i++)
			{
				var tmp = (variableValueIsArray) ? ((JArray)variableValue)[i] : variableValue;

				if (handlesAttribute == null)
				{
					object? obj = variableHelper.LoadVariables(tmp);
					if (obj == null)
					{
						continue;
					}
					if (obj is IList list && list.Count > 0)
					{
						var item = list[0];
						if (item != null && item.GetType().Name == "DapperRow")
						{
							obj = variableHelper.LoadVariables(((IDictionary<string, object>)item).Values.FirstOrDefault());
						}
					}

					elementType = (obj.GetType() == rootElementType) ? rootElementType : mainElementType;
					var objAsType = (obj.GetType() == elementType) ? obj : Convert.ChangeType(obj, elementType);

					newArray.SetValue(objAsType, i);
				}
			}

			parameterValues.Add(parameter.Name, newArray);
		}

		private void SetListParameter(ParameterInfo parameter, object variableValue, CustomAttributeData? handlesAttribute, Dictionary<string, object?> parameterValues)
		{
			if (parameter.Name == null) return;

			System.Collections.IList? list = null;
			if (VariableHelper.IsVariable(variableValue))
			{
				variableValue = variableHelper.LoadVariables(variableValue);
			}

			if (variableValue is string str && JsonHelper.IsJson(str))
			{
				if (str.TrimStart().StartsWith("{"))
				{
					var jobj = JObject.Parse(str);
					variableValue = JArray.FromObject(jobj);
				}
				else if (str.TrimStart().StartsWith("["))
				{
					variableValue = JArray.Parse(str);
				}
			}

			if (variableValue is JArray)
			{
				list = ((JArray)variableValue).ToObject(parameter.ParameterType) as System.Collections.IList;
			}
			else if (variableValue is JObject)
			{
				list = JArray.FromObject(variableValue) as System.Collections.IList;
			}
			else if (variableValue != null && variableValue.GetType().Name.StartsWith("List"))
			{
				list = (System.Collections.IList)variableValue;
			} else if (variableValue is string && Regex.IsMatch(variableValue.ToString(), "\\[(.*)\\]"))
			{
				Match match = Regex.Match(variableValue.ToString(), "\\[(.*)\\]");
				if (match.Success)
				{
					var items = match.Value.TrimStart('[').TrimEnd(']').Split(',');
					list = new List<object>();
					foreach (var item in items)
					{
						list.Add(item.Trim());
					}
				}
			} else if (!variableValue.GetType().Name.StartsWith("List"))
			{
				list = new List<object>();
				list.Add(variableValue);
			}

			if (handlesAttribute != null)
			{
				parameterValues.Add(parameter.Name, list);
				return;
			}
			

			var instanceList = Activator.CreateInstance(parameter.ParameterType);
			var addMethod = instanceList.GetType().GetMethod("Add");
			
			for (int i = 0; list != null && i < list.Count; i++)
			{
				object? obj = variableHelper.LoadVariables(list[i]);
				if (obj != null && parameter.ParameterType.GenericTypeArguments[0] == typeof(string))
				{
					addMethod.Invoke(instanceList, new object[] { obj.ToString() });
				}
				else
				{
					addMethod.Invoke(instanceList, new object[] { obj });
				}
			}
			parameterValues.Add(parameter.Name, instanceList);
		}

		private Dictionary<string, object?> MapJArray(JArray jArray)
		{
			return jArray.ToObject<List<JObject>>()
						.ToDictionary(
						jobj => jobj.Properties().First().Value.ToString(),
						jobj => (object?)jobj.Properties().Skip(1).FirstOrDefault()?.Value.ToString()
						);
		}
		private Dictionary<string, object?> MapJObject(JObject jObject)
		{
			return jObject.ToObject<Dictionary<string, object?>>();
		}

		private void SetDictionaryParameter(ParameterInfo parameter, object variableValue, CustomAttributeData? handlesAttribute, Dictionary<string, object?> parameterValues)
		{
			Dictionary<string, object?>? dict = null;
			if (VariableHelper.IsVariable(variableValue))
			{
				var obj = variableHelper.LoadVariables(variableValue);
				if (obj is JArray jArray)
				{
					foreach (JObject jobject in jArray)
					{
						dict = jobject.ToDictionary();
					}
				} else if (obj is JObject jObject)
				{
					dict = jObject.ToDictionary();
				} else
				{
					dict = obj as Dictionary<string, object?>;
				}
			}
			else
			{
				if (variableValue is JArray array)
				{
					dict = MapJArray(array);
				}
				else if (variableValue is JObject jobject)
				{
					dict = MapJObject(jobject);
				}
				else if (JsonHelper.IsJson(variableValue, out object? obj))
				{
					if (obj is JArray array2)
					{
						dict = MapJArray(array2);
					}
					else if (obj is JObject jobj)
					{
						dict = MapJObject(jobj);
					}
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

		private object? ConvertToType(object? value, ParameterInfo parameterInfo)
		{
			if (value == null) return null;
			if (parameterInfo.ParameterType == typeof(GoalToCall)) return (value == null || value.ToString() == null) ? null : new GoalToCall(value.ToString()!);
			var targetType = parameterInfo.ParameterType;
			if (targetType.Name == "String" && (value is JObject || value is JArray || value is JToken || value is JProperty))
			{
				return value.ToString();
			}

			if (targetType == null)
				throw new ArgumentNullException(nameof(targetType));

			if (value == null)
				return null;

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
				if (targetType.Name == "XmlDocument")
				{
					XmlDocument doc = new XmlDocument();
					doc.LoadXml(value.ToString());
					return doc;
				}

			}
			catch { }

			try
			{
				return Convert.ChangeType(value, targetType);
			}
			catch (Exception ex)
			{
				if (targetType.Name == "String")
				{
					return JsonConvert.SerializeObject(value, Newtonsoft.Json.Formatting.Indented);
				}
				return parameterInfo.DefaultValue;
			}
		}



		public (bool IsValid, MultipleError? Errors) IsMethodExecutionValid(MethodExecution? methodResponse, string moduleType)
		{

			var type = typeHelper.GetRuntimeType(moduleType);

			var methodInfos = type.GetMethods().Where(m => m.Name == methodResponse.MethodName);
			if (!methodInfos.Any())
				return (false,
					new MultipleError(new MethodNotFoundError($"Method {methodResponse.MethodName} not found.",
						methodResponse.MethodName, type)));

			MultipleError methodErrors = null;
			foreach (var methodInfo in methodInfos)
			{
				MultipleError me = null;
				bool validMethod = true;
				foreach (var parameter in methodInfo.GetParameters())
				{
					var obj = methodResponse.GetParameter(parameter.Name, parameter.ParameterType);
					if (obj.Error != null)
					{
						validMethod = false;
						if (me == null)
						{
							me = new MultipleError(obj.Error);
						}
						else
						{
							me.Add(obj.Error);
						}
					}
				}

				if (validMethod)
				{
					return (true, null);
				}
				else
				{
					if (methodErrors == null)
					{
						methodErrors = new MultipleError(new MethodNotMatchingWithParametersError(
							$"Method {methodInfo.Name} could not be match with parameters.", methodInfo.Name, type, me));
					}
					else
					{
						methodErrors.Add(new MethodNotMatchingWithParametersError(
							$"Method {methodInfo.Name} could not be match with parameters.", methodInfo.Name, type, me));
					}
				}
			}

			return ((methodErrors == null), methodErrors);
		}
	}
}
