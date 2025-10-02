using Jil;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.Extensions.Logging;
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
using PLang.Models.ObjectValueConverters;
using PLang.Modules.DbModule;
using PLang.Runtime;
using PLang.Services.CompilerService;
using PLang.Services.SettingsService;
using PLang.Utils.JsonConverters;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;
using System.Xml;
using static PLang.Modules.BaseBuilder;
using Parameter = PLang.Modules.BaseBuilder.Parameter;

namespace PLang.Utils;

public class MethodHelper
{
	private readonly ITypeHelper typeHelper;
	private readonly ILogger logger;
	private readonly IPLangContextAccessor contextAccessor;
	private readonly ISettings settings;
	private ConcurrentDictionary<string, MethodInfo> cachedMethodInfo;
	public MethodHelper(ITypeHelper typeHelper, ILogger logger, IPLangContextAccessor contextAccessor, ISettings settings)
	{
		this.typeHelper = typeHelper;
		this.logger = logger;
		this.contextAccessor = contextAccessor;
		this.settings = settings;
		cachedMethodInfo = new();
	}

	public async Task<MethodInfo?> GetMethod(object callingInstance, IGenericFunction function)
	{
		string cacheKey = function.Instruction.ModuleType + "_" + function.Name;
		if (function.Parameters != null)
		{
			cacheKey += "_" + string.Join(",", function.Parameters.Select(p => p.Type));
		}
		if (cachedMethodInfo.TryGetValue(cacheKey, out var methodInfo))
		{
			return methodInfo;
		}

		var methods = callingInstance.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);

		GroupedBuildErrors? error = null;
		var method = methods.FirstOrDefault(p =>
		{
			if (p.Name == function.Name)
			{
				//todo: lot of work done here at runtime
				(var _, error) = IsParameterMatch(p, function.Parameters, function.Instruction.Step);
				if (error.Count == 0) return true;
			}

			return false;
		});
		if (method != null)
		{
			cachedMethodInfo.TryAdd(cacheKey, method);
			return method;
		}

		throw new MethodNotFoundException($"Method {function.Name} could not be found that matches with your statement. " + error);
	}


	public record MethodNotFoundResponse(string Text);


	public (
		Dictionary<string, ParameterType>? ParametersProperties,
		Dictionary<string, ParameterType>? ReturnObjectProperties,
		IBuilderError? Errors
		)
			ValidateFunctions(GoalStep step, MemoryStack? memoryStack)
	{

		Dictionary<string, ParameterType>? ParameterProperties = new();
		Dictionary<string, ParameterType>? ReturnObjectProperties = new();

		var function = step.Instruction.Function;
		var module = step.ModuleType;

		var multipleError = new GroupedBuildErrors("InvalidFunction");
		if (string.IsNullOrWhiteSpace(function.Name) || function.Name.ToUpper() == "N/A")
		{
			return (null, null, new InvalidModuleError(module, $"No function in {module} matches the user intent.", function));
		}

		try
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


			foreach (var instanceFunction in instanceFunctions)
			{
				(var parameterProperties, var parameterErrors) = IsParameterMatch(instanceFunction, function.Parameters, step);
				ParameterProperties.AddOrReplace(parameterProperties);

				if (parameterErrors.Count == 0)
				{

					if (instanceFunction.ReturnType != typeof(Task) && function.ReturnValues != null && function.ReturnValues.Count > 0)
					{
						foreach (var returnValue in function.ReturnValues)
						{
							if (memoryStack != null) memoryStack.PutForBuilder(returnValue.VariableName, returnValue.Type);

							ReturnObjectProperties.Add(returnValue.VariableName,
								new ParameterType() { Name = returnValue.VariableName, FullTypeName = returnValue.Type });
						}
					}

					return (ParameterProperties, ReturnObjectProperties, null);
				}
				else
				{
					multipleError.Add(parameterErrors);
				}

			}
		}
		catch (Exception ex)
		{
			multipleError.Add(new ExceptionError(ex));
		}



		return (ParameterProperties, ReturnObjectProperties, (multipleError.Count > 0) ? multipleError : null);
	}

	bool IsNullableType(Type t) => Nullable.GetUnderlyingType(t) != null;

	public (Dictionary<string, ParameterType>? ParameterProperties, GroupedBuildErrors Error) IsParameterMatch(MethodInfo methodInfo, IReadOnlyList<Parameter> parameters, GoalStep goalStep)
	{
		GroupedBuildErrors buildErrors = new();

		Dictionary<string, ParameterType>? parameterProperties = new();

		foreach (var buildParameter in parameters ?? [])
		{
			var typeFound = methodInfo.GetParameters().FirstOrDefault(p => IsTypeMatching(p.ParameterType.FullNameNormalized(), buildParameter.Type));
			if (typeFound == null)
			{
				buildErrors.Add(new InvalidParameterError(goalStep.Instruction?.Function.Name, $"{buildParameter.Type} {buildParameter.Name} is not of the correct type.", goalStep,
					FixSuggestion: $"Make sure to format the type correctly, e.g. {typeof(string).FullNameNormalized()}, {typeof(List<string>).FullNameNormalized()}, {typeof(int?).FullNameNormalized()}"));
			}
		}
		if (buildErrors.Count > 0) return (null, buildErrors);

		foreach (var methodParameter in methodInfo.GetParameters())
		{
			try
			{

				string? parameterType = methodParameter.ParameterType.FullNameNormalized();

				// check if FullName is null, we can do much without knowing the type
				if (parameterType == null)
				{
					buildErrors.Add(new InvalidParameterError(methodInfo.Name,
						$"Parameter does not have type: {methodParameter.ParameterType}", goalStep));
					continue;
				}
				if (parameters == null)
				{
					parameters = new List<Parameter>();
				}
				var builderParameter = parameters.FirstOrDefault(x => x.Name == methodParameter.Name);

				// when builder does not provide paramater and the parameter is optional, we can stop validation
				if (builderParameter == null && (!methodParameter.HasDefaultValue || !methodParameter.IsOptional || IsNullableType(methodParameter.ParameterType)))
				{
					continue;
				}

				if (builderParameter == null && !IsNullableType(methodParameter.ParameterType) && !methodParameter.HasDefaultValue && !methodParameter.IsOptional)
				{
					buildErrors.Add(new InvalidParameterError(methodInfo.Name,
						$"{methodParameter.Name} ({parameterType}) is missing from parameters. {methodParameter.Name} is a required parameter", goalStep));

				}

				// parameter is not in build, that is ok since parameter is optional
				if (builderParameter == null) continue;

				// check if string is nullable
				if (methodParameter.ParameterType == typeof(string) && methodParameter.CustomAttributes.Count() > 0 && methodParameter.CustomAttributes.First().AttributeType.Name == "NullableAttribute" && builderParameter.Value == null)
				{
					buildErrors.Add(new InvalidParameterError(methodInfo.Name,
						$"{methodParameter.Name} ({parameterType}) is missing from parameters. {methodParameter.Name} is a required parameter", goalStep));
				}

				// lets load the type from the build, if it fails, something is not correct from LLM
				Type? builderType = Type.GetType(builderParameter.Type, false);
				if (builderType == null)
				{
					buildErrors.Add(new InvalidParameterError(methodInfo.Name,
						$"{builderParameter.Type} could not be loaded", goalStep,
						FixSuggestion: $"Make sure to format the type correctly, e.g. {typeof(string).FullNameNormalized()}, {typeof(List<string>).FullNameNormalized()}, {typeof(int?).FullNameNormalized()}"));
					continue;
				}


				if (builderType != methodParameter.ParameterType)
				{
					buildErrors.Add(new InvalidParameterError(methodInfo.Name,
						$"{builderParameter.Type} does not match with {methodParameter.ParameterType.FullNameNormalized()}", goalStep,
						FixSuggestion: $"Make sure to format the type correctly, e.g. {typeof(string).FullNameNormalized()}, {typeof(List<string>).FullNameNormalized()}, {typeof(int?).FullNameNormalized()}"));
					continue;
				}

				// sometimes llm does "\"this is text\"", prevent this
				if (builderParameter.Value != null && methodParameter.ParameterType == typeof(string) && builderParameter.Value.ToString().StartsWith("\"") && builderParameter.Value.ToString().EndsWith("\""))
				{
					buildErrors.Add(new InvalidParameterError(methodInfo.Name,
						$"{methodParameter.Name} is string, the property Value cannot start and end with quote(\").", goalStep));
				}

				// check if variable is in step, just incase LLM invents a new variable
				if (VariableHelper.IsVariable(builderParameter.Value) && !goalStep.Text.Contains(builderParameter.Value.ToString(), StringComparison.OrdinalIgnoreCase))
				{
					buildErrors.Add(new InvalidParameterError(methodInfo.Name,
						$"{builderParameter.Value} could not be found in step. User is not defining {builderParameter.Value} as variable. You should not make up new variables.", goalStep));
				}

				parameterProperties.Add(methodParameter.Name, new ParameterType() { Name = methodParameter.Name, FullTypeName = parameterType });

			}
			catch (Exception ex)
			{
				buildErrors.Add(new ExceptionError(ex, $"Exception validating method: {methodInfo.Name} and parameter: {methodParameter.Name}"));
			}

		}


		return (parameterProperties, buildErrors);
	}


	private List<ParameterType> GetParameterTypes(string parameterName, Type type)
	{
		List<ParameterType> objectProperties = new();


		var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

		foreach (var prop in properties)
		{
			objectProperties.Add(new ParameterType() { FullTypeName = prop.PropertyType.FullNameNormalized(), Name = prop.Name });
		}

		return objectProperties;
	}

	private bool IsTypeMatching(string methodParameterType, string buildParamType)
	{
		bool isSame = methodParameterType == buildParamType;
		if (isSame) return true;

		return false;
	}
	private string NormalizeType(Type type)
	{
		if (type.Name.StartsWith("Nullable"))
		{
			return NormalizeType(type.GenericTypeArguments[0].FullName);
		}
		return NormalizeType(type.FullName);
	}
	private string NormalizeType(string type)
	{
		if (!type.Contains(',') && !type.Contains("`")) return type;


		var normalizedType = type;
		if (type.Contains(',')) normalizedType = type.Substring(0, type.IndexOf(','));

		if (normalizedType.Contains("[["))
		{
			normalizedType = normalizedType.Replace("`1", "").Replace("`2", "").Replace("`3", "").Replace("[[", "<") + ">";
		}

		return normalizedType;



	}

	public (Dictionary<string, object?> Parameters, IError? Error) GetParameterValues(MethodInfo method, IGenericFunction function)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		var parameterValues = new Dictionary<string, object?>();
		var parameters = method.GetParameters();
		if (parameters.Length == 0) return (parameterValues, null);

		var step = function.Instruction.Step;
		var goal = function.Instruction.Step.Goal;
		var memoryStack = contextAccessor.Current.MemoryStack;

		foreach (var parameter in parameters)
		{
			logger.LogDebug($"         - Loading parameter {parameter.Name} - {stopwatch.ElapsedMilliseconds}");
			if (parameter.Name == null) continue;

			var inputParameter = function.Parameters.FirstOrDefault(p => p.Name == parameter.Name);

			if (inputParameter == null && !parameter.IsOptional && !parameter.ParameterType.Name.StartsWith("Nullable"))
			{
				throw new ParameterException($"Could not find parameter {parameter.Name}", step);
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
					if (VariableHelper.IsSetting(variableValue.ToString()))
					{
						string setting = settings.GetOrDefault<string>(typeof(Settings), variableValue.ToString().Replace("Settings.", ""), "");
						parameterValues.Add(parameter.Name, setting);
						continue;
					}
					var ov = memoryStack.GetObjectValue(variableValue.ToString());
					if (ov != null && ov.Value != null && parameter.ParameterType == typeof(ObjectValue))
					{
						parameterValues.Add(parameter.Name, ov);
						continue;
					}
					else if (ov != null && ov.Initiated && ov.Value == null)
					{
						parameterValues.Add(parameter.Name, ov.Value);
						continue;
					}
					else if (!ov.Initiated && function.Instruction.Step.ModuleType != "PLang.Modules.ConditionalModule")
					{
						logger.LogWarning($"{variableValue} does not exist - {step.LineNumber}:{step.Text}");
					}

				}

				if (parameter.ParameterType.Name.StartsWith("Dictionary") || parameter.ParameterType.Name.StartsWith("IDictionary"))
				{
					if (parameter.ParameterType.ToString().StartsWith("System.Collections.Generic.Dictionary`2[System.String,System.Tuple`2["))
					{
						SetDictionaryWithTupleParameter(parameter, variableValue, handlesAttribute, parameterValues, memoryStack);

					}
					else
					{
						SetDictionaryParameter(parameter, variableValue, handlesAttribute, parameterValues, memoryStack);
					}
				}
				else if (parameter.ParameterType.Name.StartsWith("List") || parameter.ParameterType.Name.StartsWith("IList"))
				{
					SetListParameter(parameter, variableValue, handlesAttribute, parameterValues, memoryStack);

				}
				else if (parameter.ParameterType.IsArray)
				{
					SetArrayParameter(parameter, variableValue, handlesAttribute, parameterValues, memoryStack);
				}
				else
				{
					SetObjectParameter(parameter, variableValue, handlesAttribute, parameterValues, memoryStack);
				}
				logger.LogDebug($"         - Have parameter {parameter.Name} - {stopwatch.ElapsedMilliseconds}");

			}
			catch (PropertyNotFoundException) { throw; }
			catch (Exception ex)
			{
				if (ex is AskUserError) throw;

				return (parameterValues, new InvalidParameterError(function.Name, $"Cannot convert {inputParameter?.Value} on parameter {parameter.Name} - value:{variableValue}", step, Exception: ex));
			}

		}
		logger.LogDebug($"         - returning parameterVAlues - {stopwatch.ElapsedMilliseconds}");
		return (parameterValues, null);
	}


	private void SetObjectParameter(ParameterInfo parameter, object variableValue, CustomAttributeData? handlesAttribute, Dictionary<string, object?> parameterValues, MemoryStack memoryStack)
	{

		object? value = variableValue;
		if (handlesAttribute == null)
		{
			if (variableValue is JObject jobj)
			{
				string str = variableValue.ToString();
				if (!str.Contains("%Settings."))
				{
					var newJobj = jobj.DeepClone();
					newJobj.ResolvePlaceholders(parameter.ParameterType, (name, targetType) =>
					{
						var value = memoryStack.Get(name);
						if (value is ObjectValue ov) return ov.Value;
						if (value is List<object> list && list.FirstOrDefault() is ObjectValue)
						{
							return list.Select(p => ((ObjectValue)p).Value);
						}

						if (targetType != null)
						{
							return TypeHelper.ConvertToType(value, targetType, variableName: name);
						}


						return value;
					});

					int b = 0;
					try
					{
						var obj = newJobj.ToObject(parameter.ParameterType);
						parameterValues.Add(parameter.Name!, obj);
					}
					catch (Exception ex)
					{
						b = 1;
					}


					if (b == 1) throw new Exception("Erro");

					
					return;
				}
			}

			value = memoryStack.LoadVariables(variableValue);
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



	private void SetArrayParameter(ParameterInfo parameter, object variableValue, CustomAttributeData? handlesAttribute, Dictionary<string, object?> parameterValues, MemoryStack memoryStack)
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
			var value = (handlesAttribute != null) ? variableValue : memoryStack.LoadVariables(variableValue);
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
			parameterValues.Add(parameter.Name, memoryStack.LoadVariables(variableValue));
			return;
		}


		Array newArray = Array.CreateInstance(rootElementType, arrayLength);
		for (int i = 0; i < arrayLength; i++)
		{
			var tmp = (variableValueIsArray) ? ((JArray)variableValue)[i] : variableValue;

			if (handlesAttribute == null)
			{
				object? obj = memoryStack.LoadVariables(tmp);
				if (obj == null)
				{
					continue;
				}
				if (obj is IList list && list.Count > 0)
				{
					var item = list[0];
					if (item is Row row)
					{
						obj = memoryStack.LoadVariables(row.Values.FirstOrDefault());
					}
					else
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

	private void SetListParameter(ParameterInfo parameter, object? obj, CustomAttributeData? handlesAttribute, Dictionary<string, object?> parameterValues, MemoryStack memoryStack)
	{
		if (parameter.Name == null) return;
		string typeName = parameter.ParameterType.Name;
		if (parameter.ParameterType.GenericTypeArguments.Length > 0 && parameter.ParameterType.GenericTypeArguments[0] == typeof(ObjectValue))
		{
			List<ObjectValue> ovList = new();
			if (obj is IList varList)
			{
				foreach (var item in varList)
				{
					var ov = memoryStack.GetObjectValue(item.ToString());
					if (ov == null || !ov.Initiated) throw new ArgumentException($"{item.ToString()} has not been defined");

					ovList.Add(ov);
				}
			}
			else
			{
				var ov = memoryStack.GetObjectValue(obj.ToString());
				if (ov == null || !ov.Initiated) throw new ArgumentException($"{obj.ToString()} has not been defined");
				ovList.Add(ov);
			}
			parameterValues.Add(parameter.Name, ovList);
			return;
		}
		System.Collections.IList? list = null;

		if (obj is string variableName && VariableHelper.IsVariable(variableName))
		{
			var value = memoryStack.GetObjectValue(variableName).ValueAs(parameter.ParameterType);

			parameterValues.Add(parameter.Name, value);
			return;
		}


		object? variableValue = null;
		if (obj is JArray jArray)
		{
			list = jArray.ToObject(parameter.ParameterType) as IList;

		}
		else if (obj is JObject jObject)
		{
			list = JArray.FromObject(jObject) as System.Collections.IList;
		}
		else if (obj is IList)
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
				list[i] = memoryStack.LoadVariables(list[i]);
			}
			parameterValues.Add(parameter.Name, list);
			return;
		}

		var instanceList = (parameter.ParameterType.Name.StartsWith("IList")) ? new List<object>() : Activator.CreateInstance(parameter.ParameterType);
		var addMethod = instanceList.GetType().GetMethod("Add");

		for (int i = 0; list != null && i < list.Count; i++)
		{
			object? objInstance = memoryStack.LoadVariables(list[i]);


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

	private Dictionary<string, object?> MapJArray(JArray jArray, Type paramType)
	{
		return jArray.ToObject<List<JObject>>()
					.ToDictionary(
					jobj => jobj.Properties().First().Value.ToString(),
					jobj => TypeHelper.ConvertToType(jobj.Properties().Skip(1).FirstOrDefault()?.Value, paramType)
					);
	}
	private IDictionary MapJObject(JObject jObject, Type paramType)
	{
		var valueType = paramType.GetGenericArguments()[1];
		var dictType = typeof(Dictionary<,>).MakeGenericType(typeof(string), valueType);
		var method = typeof(JToken)
			.GetMethods()
			.First(m => m.Name == "ToObject" && m.IsGenericMethod && m.GetParameters().Length == 0);

		var genericMethod = method.MakeGenericMethod(dictType);
		var typedDict = genericMethod.Invoke(jObject, null);
		return typedDict as IDictionary;


		//return jObject.ToObject<Dictionary<string, object?>>();
	}


	private void SetDictionaryWithTupleParameter(ParameterInfo parameter, object variableValue, CustomAttributeData? handlesAttribute, Dictionary<string, object?> parameterValues, MemoryStack memoryStack)
	{
		Dictionary<string, Tuple<object?, object?>?>? dict = null;
		if (VariableHelper.IsVariable(variableValue))
		{
			var obj = memoryStack.LoadVariables(variableValue);
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
				/*try
				{
					dict = jobject.ToObject<Dictionary<string, Tuple<object?, object?>?>>();
				}
				catch (Exception)
				{*/
				var itemWithList = jobject.ToObject<Dictionary<string, List<object?>?>>();
				//if (itemWithList == null) throw;

				dict = new();
				foreach (var item in itemWithList)
				{
					if (item.Value == null) continue;

					if (item.Value.Count > 1)
					{
						dict.Add(item.Key, new Tuple<object?, object?>(item.Value[0], item.Value[1]));
					}
					else if (item.Value.Count > 0)
					{
						dict.Add(item.Key, new Tuple<object?, object?>(item.Value[0], null));
					}
				}
				//}
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
			dict[item.Key] = new Tuple<object?, object?>(memoryStack.LoadVariables(item.Value?.Item1), memoryStack.LoadVariables(item.Value?.Item2));
		}
		parameterValues.Add(parameter.Name, dict);

	}

	private void SetDictionaryParameter(ParameterInfo parameter, object variableValue, CustomAttributeData? handlesAttribute, Dictionary<string, object?> parameterValues, MemoryStack memoryStack)
	{
		IDictionary? dict = null;
		if (VariableHelper.IsVariable(variableValue))
		{
			var obj = memoryStack.LoadVariables(variableValue);
			if (obj is JArray jArray)
			{
				dict = MapJArray(jArray, parameter.ParameterType);
			}
			else if (obj is JObject jObject)
			{
				dict = MapJObject(jObject, parameter.ParameterType);
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
					dict = GetInstanceOfDictionaryTyped(parameter.ParameterType);
					dict.Add(str.Replace("%", ""), obj);
				}
			}
		}
		else
		{
			if (variableValue is JArray array)
			{
				dict = MapJArray(array, parameter.ParameterType);
			}
			else if (variableValue is JObject jobject)
			{
				dict = MapJObject(jobject, parameter.ParameterType);
			}
			else if (JsonHelper.IsJson(variableValue, out object? obj))
			{
				if (obj is JArray array2)
				{
					dict = MapJArray(array2, parameter.ParameterType);
				}
				else if (obj is JObject jobj)
				{
					dict = MapJObject(jobj, parameter.ParameterType);
				}
			}
		}
		if (dict == null) dict = GetInstanceOfDictionaryTyped(parameter.ParameterType);

		if (handlesAttribute != null)
		{
			parameterValues.Add(parameter.Name, dict);
			return;
		}

		foreach (DictionaryEntry item in dict)
		{
			dict[item.Key] = memoryStack.LoadVariables(item.Value);
		}
		parameterValues.Add(parameter.Name, dict);
	}

	private IDictionary? GetInstanceOfDictionaryTyped(Type paramType)
	{

		var valueType = paramType.GetGenericArguments()[1];
		var dictType = typeof(Dictionary<,>).MakeGenericType(typeof(string), valueType);
		var dictInstance = Activator.CreateInstance(dictType);
		return dictInstance as IDictionary;
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
				parameterValues.Add(parameter.Name, null);
			}
		}
	}

	private object? ConvertToType(object? value, ParameterInfo parameterInfo)
	{
		return TypeHelper.ConvertToType(value, parameterInfo.ParameterType);
		/*
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
			try
			{
				var jsonSerializer = new JsonSerializerSettings()
				{
					NullValueHandling = NullValueHandling.Ignore,
					DefaultValueHandling = DefaultValueHandling.Populate,
				};

				var json = JsonConvert.SerializeObject(value);
				return JsonConvert.DeserializeObject(json, targetType, jsonSerializer);
			}
			catch { 
				if (targetType.Name == "String")
				{
					return StringHelper.ConvertToString(value);
				}
				return parameterInfo.DefaultValue;
			}

		}*/
	}
}

