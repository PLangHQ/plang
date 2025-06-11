using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Attributes;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Errors.Handlers;
using PLang.Errors.Methods;
using PLang.Exceptions;
using PLang.Models;
using PLang.Models.ObjectValueConverters;
using PLang.Modules.DbModule;
using PLang.Runtime;
using PLang.Services.CompilerService;
using System.Collections;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;
using System.Xml;
using static PLang.Modules.BaseBuilder;
using Parameter = PLang.Modules.BaseBuilder.Parameter;

namespace PLang.Utils
{
	public class MethodHelper
	{
		private GoalStep goalStep;
		private readonly VariableHelper variableHelper;
		private readonly ITypeHelper typeHelper;

		public MethodHelper(GoalStep goalStep, VariableHelper variableHelper, ITypeHelper typeHelper)
		{
			this.goalStep = goalStep;
			this.variableHelper = variableHelper;
			this.typeHelper = typeHelper;
		}

		public async Task<MethodInfo?> GetMethod(object callingInstance, IGenericFunction function)
		{
			var methods = callingInstance.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);

			GroupedBuildErrors? error = null;
			var method = methods.FirstOrDefault(p =>
			{
				if (p.Name == function.Name)
				{
					//todo: lot of work done here at runtime
					(var _, error) = IsParameterMatch(p, function.Parameters, goalStep);
					if (error.Count == 0) return true;
				}

				return false;
			});
			if (method != null) return method;

			throw new MethodNotFoundException($"Method {function.Name} could not be found that matches with your statement. " + error);
		}


		public record MethodNotFoundResponse(string Text);


		public (
			Dictionary<string, List<ParameterType>>? ParametersProperties,
			Dictionary<string, List<ParameterType>>? ReturnObjectProperties,
			IBuilderError? Errors
			)
				ValidateFunctions(IGenericFunction function, string module, MemoryStack? memoryStack)
		{
			Dictionary<string, List<ParameterType>>? ParameterProperties = new();
			Dictionary<string, List<ParameterType>>? ReturnObjectProperties = new();

			var multipleError = new GroupedBuildErrors("InvalidFunction");
			if (string.IsNullOrWhiteSpace(function.Name) || function.Name.ToUpper() == "N/A")
			{
				return (null, null, new InvalidModuleError(module, $"No function in {module} matches the user intent.", function));
			}
			else
			{
				var runtimeType = typeHelper.GetRuntimeType(module);
				if (runtimeType == null)
				{
					return (null, null, new InvalidModuleError(module, $"Could not load {module}.Program", function));
				}

				var instanceFunctions = runtimeType.GetMethods().Where(p => p.Name == function.Name);
				if (instanceFunctions.Count() == 0)
				{
					return (null, null, new InvalidFunctionsError(function.Name, $"Could not find {function.Name} in module", false));
				}
				else
				{

					foreach (var instanceFunction in instanceFunctions)
					{
						(var parameterProperties, var parameterErrors) = IsParameterMatch(instanceFunction, function.Parameters, goalStep);
						ParameterProperties.AddOrReplace(parameterProperties);

						if (parameterErrors.Count == 0)
						{
							if (instanceFunction.ReturnType != typeof(Task) && function.ReturnValues != null && function.ReturnValues.Count > 0)
							{
								foreach (var returnValue in function.ReturnValues)
								{
									if (memoryStack != null) memoryStack.PutForBuilder(returnValue.VariableName, returnValue.Type);

									var objectProperties = GetParameterTypes(returnValue.VariableName, returnValue.Type);
									ReturnObjectProperties.Add(returnValue.VariableName, objectProperties);
								}
							}
						}
						else
						{
							multipleError.Add(parameterErrors);
						}

					}

				}
			}


			return (ParameterProperties, ReturnObjectProperties, (multipleError.Count > 0) ? multipleError : null);
		}

		private bool ParamTypeIsOk(string methodParamType, string buildParamType)
		{
			if (buildParamType.StartsWith("System.") || buildParamType.StartsWith("Dictionary<") || buildParamType.StartsWith("List<")) return true;
			return (methodParamType == buildParamType);
		}

		public (Dictionary<string, List<ParameterType>>? ParameterProperties, GroupedBuildErrors Error) IsParameterMatch(MethodInfo p, List<Parameter> parameters, GoalStep goalStep)
		{
			GroupedBuildErrors buildErrors = new();

			var variablesInStep = variableHelper.GetVariables(goalStep.Text);
			Dictionary<string, List<ParameterType>>? parameterProperties = new();

			foreach (var buildParameter in parameters ?? [])
			{
				var typeFound = p.GetParameters().FirstOrDefault(p => ParamTypeIsOk(CleanAssemblyInfo(p.ParameterType.FullName), buildParameter.Type));
				if (typeFound == null)
				{
					buildErrors.Add(new InvalidParameterError(goalStep.Instruction?.Function.Name, $"Could not find {buildParameter.Type} in method {p.Name}", goalStep));
				}
			}


			foreach (var methodParameter in p.GetParameters())
			{
				var parameterType = methodParameter.ParameterType.Name.ToLower();
				if (parameterType.Contains("`")) parameterType = parameterType.Substring(0, parameterType.IndexOf("`"));

				var parameter = parameters.FirstOrDefault(x => x.Name == methodParameter.Name);

				if (parameter == null && parameterType != "nullable" && !methodParameter.HasDefaultValue && !methodParameter.IsOptional)
				{
					buildErrors.Add(new InvalidParameterError(p.Name,
						$"{methodParameter.Name} ({parameterType}) is missing from parameters. {methodParameter.Name} is a required parameter", goalStep));
				}
				else if (parameter != null && parameterType == "string" && methodParameter.CustomAttributes.Count() > 0 && methodParameter.CustomAttributes.First().AttributeType.Name == "NullableAttribute" && parameter.Value == null)
				{
					buildErrors.Add(new InvalidParameterError(p.Name,
						$"{methodParameter.Name} ({parameterType}) is missing from parameters. {methodParameter.Name} is a required parameter", goalStep));
				}

				if (parameter != null && parameter.Value != null && parameterType == "string" && parameter.Value.ToString().StartsWith("\"") && parameter.Value.ToString().EndsWith("\""))
				{
					buildErrors.Add(new InvalidParameterError(p.Name,
						$"{methodParameter.Name} is string, the property Value cannot start and end with quote(\").", goalStep));
				}

				if (parameter != null && VariableHelper.IsVariable(parameter.Value) && !variablesInStep.Any(p => p.PathAsVariable.Equals(parameter.Value?.ToString(), StringComparison.OrdinalIgnoreCase)))
				{
					buildErrors.Add(new InvalidParameterError(p.Name,
						$"{parameter.Value} could not be found in step. User is not defining {parameter.Value} as variable.", goalStep));
				}
				if (parameterType == "nullable" && methodParameter.ParameterType.GenericTypeArguments.Length > 0)
				{
					parameterType = methodParameter.ParameterType.GenericTypeArguments[0].Name.ToLower();
				}

				string? parameterTypeName = methodParameter.ParameterType.FullName;
				if (parameterTypeName == null)
				{
					buildErrors.Add(new InvalidParameterError(p.Name,
						$"Parameter does not have type: {methodParameter.ParameterType}", goalStep));
				}

				if (parameterTypeName == "System.Object")
				{
					continue;
				}


				var objectProperties = GetParameterTypes(methodParameter.Name, parameterTypeName);
				parameterProperties.Add(methodParameter.Name, objectProperties);


				if (parameter != null && parameter.Type == CleanAssemblyInfo(parameterTypeName))
				{
					continue;
				}

				if (methodParameter.IsOptional) continue;

				if (parameters.FirstOrDefault(p => p.Type.ToLower().StartsWith(parameterType)) == null && parameters.FirstOrDefault(p => p.Type.ToLower() == parameterTypeName!.ToLower()) == null)
				{

					// temp thing, should be removed
					if (parameterTypeName == "PLang.Models.GoalToCall")
					{
						parameterTypeName = "String";
						if (parameters.FirstOrDefault(p => p.Type.ToLower().StartsWith(parameterTypeName)) == null && parameters.FirstOrDefault(p => p.Type.ToLower() == parameterTypeName!.ToLower()) == null)
						{
							buildErrors.Add(new InvalidParameterError(p.Name,
								$"{methodParameter.Name} ({methodParameter.ParameterType.Name}) is missing", goalStep));
						}
					}
					else if (!methodParameter.ParameterType.Name.ToLower().StartsWith("nullable") && !methodParameter.IsOptional && !methodParameter.HasDefaultValue)
					{
						buildErrors.Add(new InvalidParameterError(p.Name,
								$"{methodParameter.Name} ({methodParameter.ParameterType.Name}) is missing\n", goalStep));
					}
				}

			}
			return (parameterProperties, buildErrors);
		}


		private List<ParameterType> GetParameterTypes(string parameterName, string parameterTypeName)
		{
			List<ParameterType> objectProperties = new();
			var type = Type.GetType(parameterTypeName, throwOnError: false);
			if (type == null) return objectProperties;
			if (type.Name.StartsWith("List`") || type.Name.StartsWith("Dictionary`") || type.Name.StartsWith("Tuple`"))
			{
				type = type.BaseType;
			}
			if (type == null || TypeHelper.IsConsideredPrimitive(type) || type.FullName == "System.Object")
			{
				objectProperties.Add(new ParameterType() { FullTypeName = type.FullName, Name = parameterName });
				return objectProperties;
			}


			var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

			foreach (var prop in properties)
			{
				objectProperties.Add(new ParameterType() { FullTypeName = prop.PropertyType.FullName, Name = prop.Name });
			}

			return objectProperties;
		}

		private string CleanAssemblyInfo(string parameterTypeName)
		{
			if (!parameterTypeName.Contains("`")) return parameterTypeName;

			var newParamType = parameterTypeName.Substring(0, parameterTypeName.IndexOf(','));
			return newParamType.Replace("[[", "[") + "]";
		}

		public Dictionary<string, object?> GetParameterValues(MethodInfo method, IGenericFunction function)
		{
			var parameterValues = new Dictionary<string, object?>();
			var parameters = method.GetParameters();
			if (parameters.Length == 0) return parameterValues;

			JsonConvert.DefaultSettings = () => new JsonSerializerSettings
			{
				Converters = { new JsonObjectValueConverter() }
			};

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
						var ov = variableHelper.GetObjectValue(variableValue.ToString(), goal: goalStep.Goal);
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

					if (parameter.ParameterType.Name.StartsWith("Dictionary") || parameter.ParameterType.Name.StartsWith("IDictionary"))
					{
						if (parameter.ParameterType.ToString().StartsWith("System.Collections.Generic.Dictionary`2[System.String,System.Tuple`2["))
						{
							SetDictionaryWithTupleParameter(parameter, variableValue, handlesAttribute, parameterValues);

						}
						else
						{
							SetDictionaryParameter(parameter, variableValue, handlesAttribute, parameterValues);
						}
					}
					else if (parameter.ParameterType.Name.StartsWith("List") || parameter.ParameterType.Name.StartsWith("IList"))
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

				if (!TypeHelper.IsRecordOrAnonymousType(value) && parameter.ParameterType != typeof(string) && parameter.ParameterType == typeof(object) && value != null && JsonHelper.IsJson(value.ToString()))
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
				variableValue = new JArray(variableValue);
				variableValueIsArray = true;
			}

			int arrayLength = variableValueIsArray ? ((JArray)variableValue).Count : 1;
			var rootElementType = parameter.ParameterType.GetElementType();
			var mainElementType = parameter.ParameterType;
			Type elementType;
			if (mainElementType.IsArray && variableValueIsArray)
			{
				var value = (handlesAttribute != null) ? variableValue : variableHelper.LoadVariables(variableValue);
				if (value is JArray array)
				{
					parameterValues.Add(parameter.Name, array.ToObject(mainElementType));
					return;
				}
				else if (value is string && JsonHelper.IsJson(value, out object strArray))
				{
					parameterValues.Add(parameter.Name, ((JToken)strArray).ToObject(mainElementType));
					return;
				}
				else if (value.GetType().IsArray)
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
						if (item is Row row)
						{
							obj = variableHelper.LoadVariables(row.Values.FirstOrDefault());
						} else
						{
							throw new Exception("Why no load here?");
						}

					}

					elementType = (obj.GetType() == rootElementType) ? rootElementType : mainElementType;
					var objAsType = (obj.GetType() == elementType) ? obj : Convert.ChangeType(obj, elementType);

					newArray.SetValue(objAsType, i);
				}
			}

			parameterValues.Add(parameter.Name, newArray);
		}

		private void SetListParameter(ParameterInfo parameter, object? obj, CustomAttributeData? handlesAttribute, Dictionary<string, object?> parameterValues)
		{
			if (parameter.Name == null) return;
			string typeName = parameter.ParameterType.Name;

			System.Collections.IList? list = null;
			
			if (obj is string variableName && VariableHelper.IsVariable(variableName))
			{
				var value = variableHelper.GetValue(variableName, parameter.ParameterType);
				
				parameterValues.Add(parameter.Name, value);
				return;
			}


			object? variableValue = null;
			if (obj is JArray jArray)
			{
				list = jArray.ToObject(parameter.ParameterType) as System.Collections.IList;
			} else if (obj is JObject jObject)
			{
				list = JArray.FromObject(jObject) as System.Collections.IList;
			} else if (obj is IList)
			{
				list = (System.Collections.IList)obj;
			}
			else if (obj is string str)
			{
				if (JsonHelper.IsJson(str, out variableValue))
				{
					//is json object
					if (variableValue is JArray jArray2)
					{
						list = jArray2.ToObject(parameter.ParameterType) as System.Collections.IList;
					}
					else if (variableValue is JObject jObject2)
					{
						list = JArray.FromObject(jObject2) as System.Collections.IList;
					}
				}
				else if (Regex.IsMatch(str, "\\[(.*)\\]"))
				{
					Match match = Regex.Match(str, "\\[(.*)\\]");
					if (match.Success)
					{
						var items = match.Value.TrimStart('[').TrimEnd(']').Split(',');
						list = new List<object>();
						foreach (var item in items)
						{
							list.Add(item.Trim());
						}
					}
				}
			}
			else
			{
				if (parameter.ParameterType.Name.StartsWith("IList"))
				{
					list = new List<object>();
				}
				else
				{
					list = Activator.CreateInstance(parameter.ParameterType) as IList;
					if (list == null) list = new List<object>();
				}
				list.Add(obj);
			}

			if (handlesAttribute != null)
			{
				parameterValues.Add(parameter.Name, list);
				return;
			}
			if (list == null || list.Count == 0)
			{
				parameterValues.Add(parameter.Name, list);
				return;
			}
			if (list.GetType() == parameter.ParameterType)
			{
				for (int i = 0; i < list.Count; i++)
				{
					list[i] = variableHelper.LoadVariables(list[i]);
				}
				parameterValues.Add(parameter.Name, list);
				return;
			}

			var instanceList = (parameter.ParameterType.Name.StartsWith("IList")) ? new List<object>() : Activator.CreateInstance(parameter.ParameterType);
			var addMethod = instanceList.GetType().GetMethod("Add");

			for (int i = 0; list != null && i < list.Count; i++)
			{
				object? objInstance = variableHelper.LoadVariables(list[i]);


				if (objInstance != null && parameter.ParameterType.GenericTypeArguments.Count() > 0 && parameter.ParameterType.GenericTypeArguments[0] == typeof(string))
				{
					addMethod.Invoke(instanceList, new object[] { objInstance.ToString() });
				}
				else
				{
					addMethod.Invoke(instanceList, new object[] { objInstance });
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


		private void SetDictionaryWithTupleParameter(ParameterInfo parameter, object variableValue, CustomAttributeData? handlesAttribute, Dictionary<string, object?> parameterValues)
		{
			Dictionary<string, Tuple<object?, object?>?>? dict = null;
			if (VariableHelper.IsVariable(variableValue))
			{
				var obj = variableHelper.LoadVariables(variableValue);
				if (obj is JArray jArray)
				{
					foreach (JObject jobject in jArray)
					{
						dict = jobject.ToObject<Dictionary<string, Tuple<object?, object?>?>>();
					}
				}
				else if (obj is JObject jObject)
				{
					dict = jObject.ToObject<Dictionary<string, Tuple<object?, object?>?>>();
				}
				else
				{
					dict = obj as Dictionary<string, Tuple<object?, object?>?>;
				}
			}
			else
			{
				if (variableValue is JArray array)
				{
					throw new NotImplementedException("Need to implement this");
				}
				else if (variableValue is JObject jobject)
				{
					try
					{
						dict = jobject.ToObject<Dictionary<string, Tuple<object?, object?>?>>();
					}
					catch (Exception)
					{
						var itemWithList = jobject.ToObject<Dictionary<string, List<object?>?>>();
						if (itemWithList == null) throw;

						dict = new();
						foreach (var item in itemWithList)
						{
							if (item.Value == null) continue;

							if (item.Value.Count > 1)
							{
								dict.Add(item.Key, new Tuple<object?, object?>(item.Value[0], item.Value[1]));
							} else if (item.Value.Count > 0)
							{
								dict.Add(item.Key, new Tuple<object?, object?>(item.Value[0], null));
							}
						}
					}
				}
				else if (JsonHelper.IsJson(variableValue, out object? obj))
				{
					if (obj is JArray array2)
					{
						throw new NotImplementedException("Need to implement this");
					}
					else if (obj is JObject jobj)
					{
						dict = jobj.ToObject<Dictionary<string, Tuple<object?, object?>?>>();
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
				dict[item.Key] = new Tuple<object?, object?>(variableHelper.LoadVariables(item.Value?.Item1), variableHelper.LoadVariables(item.Value?.Item2));
			}
			parameterValues.Add(parameter.Name, dict);

		}

		private void SetDictionaryParameter(ParameterInfo parameter, object variableValue, CustomAttributeData? handlesAttribute, Dictionary<string, object?> parameterValues)
		{
			Dictionary<string, object?>? dict = null;
			if (VariableHelper.IsVariable(variableValue))
			{
				var obj = variableHelper.LoadVariables(variableValue);
				if (obj is JArray jArray)
				{
					dict = MapJArray(jArray);
				}
				else if (obj is JObject jObject)
				{
					dict = jObject.ToDictionary();
				}
				else
				{
					dict = obj as Dictionary<string, object?>;
					if (dict == null && obj is string strJson && JsonHelper.LookAsJsonScheme(strJson))
					{
						try
						{
							dict = JObject.Parse(strJson).ToDictionary();
						}
						catch { }
					}
					if (dict == null && variableValue is string str)
					{
						dict = new();
						dict.Add(str.Replace("%", ""), obj);
					}
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

			if (parameter.ParameterType != null)
			{
				int i = 0;
			}

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
			if (value is JObject jobj && parameterInfo.ParameterType == typeof(GoalToCallInfo))
			{
				int i = 0;
				var o  = jobj.ToObject<GoalToCallInfo>();
			}

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
				if (value is JToken token)
				{
					var jsonSerializer = new JsonSerializer()
					{
						NullValueHandling = NullValueHandling.Ignore,
						DefaultValueHandling = DefaultValueHandling.Populate,
					};
					return token.ToObject(targetType, jsonSerializer);
				}

				return Convert.ChangeType(value, targetType);
			}
			catch (Exception ex)
			{
				if (targetType.Name == "String")
				{
					return StringHelper.ConvertToString(value);
				}
				return parameterInfo.DefaultValue;
			}
		}
	}
}
