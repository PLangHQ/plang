using Epiforge.Extensions.Components;
using Nethereum.ABI.CompilationMetadata;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Modules;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Linq;
using Websocket.Client.Logging;
using static PLang.Modules.DbModule.Program;

namespace PLang.Utils;
public interface ITypeHelper
{
	BaseBuilder GetInstructionBuilderInstance(string module);
	List<Type> GetRuntimeModules();
	string GetModulesAsString(List<string>? excludedModules = null);
	BaseProgram GetProgramInstance(string module);
	List<Type> GetBuilderModules();
	Type? GetBuilderType(string module);
	Type? GetRuntimeType(string? module);
	List<Type> GetTypesByType(Type type);
	Task<object?> Run(string @namespace, string @class, string method, Dictionary<string, object?>? parameters);
}

public class TypeHelper : ITypeHelper
{
	private readonly IPLangFileSystem fileSystem;
	private static List<Type> runtimeModules = new List<Type>();
	private static List<Type> builderModules = new List<Type>();

	public TypeHelper(IPLangFileSystem fileSystem, DependancyHelper dependancyHelper)
	{
		runtimeModules = dependancyHelper.LoadModules(typeof(BaseProgram), fileSystem.GoalsPath);
		builderModules = dependancyHelper.LoadModules(typeof(BaseBuilder), fileSystem.GoalsPath);

		this.fileSystem = fileSystem;
	}

	private static Version GetAssemblyVersion(string filePath)
	{
		try
		{
			var assemblyName = AssemblyName.GetAssemblyName(filePath);
			return assemblyName.Version;
		}
		catch
		{
			return new Version(0, 0, 0, 0); // Return a default version if the assembly version cannot be determined
		}
	}

	public List<Type> GetTypesByType(Type type)
	{
		List<Type> types;
		var executingAssembly = Assembly.GetExecutingAssembly();
		if (type.IsInterface || type.IsAbstract)
		{
			types = executingAssembly.GetTypes().Where(t => type.IsAssignableFrom(t)).ToList();
		}
		else
		{
			types = executingAssembly.GetTypes().Where(t => t == type).ToList();
		}

		string modulesDirectory = Path.Join(fileSystem.GoalsPath, ".modules");
		if (fileSystem.Directory.Exists(modulesDirectory))
		{
			List<string> loaded = new();
			foreach (var dll in fileSystem.Directory.GetFiles(modulesDirectory, "*.dll", SearchOption.AllDirectories))
			{
				Assembly loadedAssembly = Assembly.LoadFile(dll);
				List<Type> typesFromAssembly;

				try
				{
					if (type.IsInterface || type.IsAbstract)
					{
						typesFromAssembly = loadedAssembly.GetTypes().Where(t => type.IsAssignableFrom(t)).ToList();
					}
					else
					{
						typesFromAssembly = loadedAssembly.GetTypes().Where(t => t == type).ToList();
					}

					if (typesFromAssembly.Count > 0)
					{
						// Add the found types to the main list
						types.AddRange(typesFromAssembly);
					}
				}
				catch (Exception ex)
				{
					//Console.WriteLine(ex.Message);
					continue;
				}
			}
		}

		string servicesDirectory = Path.Join(fileSystem.GoalsPath, ".services");
		if (fileSystem.Directory.Exists(servicesDirectory))
		{
			foreach (var dll in fileSystem.Directory.GetFiles(servicesDirectory, "*.dll", SearchOption.AllDirectories))
			{

				// Load the assembly
				Assembly loadedAssembly = Assembly.LoadFile(dll);
				List<Type> typesFromAssembly;
				if (type.IsInterface || type.IsAbstract)
				{
					typesFromAssembly = loadedAssembly.GetTypes().Where(t => type.IsAssignableFrom(t)).ToList();
				}
				else
				{
					typesFromAssembly = loadedAssembly.GetTypes().Where(t => t == type).ToList();
				}

				if (typesFromAssembly.Count > 0)
				{
					// Add the found types to the main list
					types.AddRange(typesFromAssembly);
				}
			}
		}
		types = types.GroupBy(p => p.FullName).Select(p => p.FirstOrDefault()).ToList();

		return types;
	}


	public string GetModulesAsString(List<string> excludedModules)
	{
		string strModules = "[";
		foreach (var module in runtimeModules)
		{
			if (excludedModules != null && excludedModules.Contains(module.FullName.Replace(".Program", ""))) continue;

			strModules += $"{{ \"module\": \"{module.FullName.Replace(".Program", "")}\"";


			var descriptions = module.CustomAttributes.Where(p => p.AttributeType.Name == "DescriptionAttribute");
			if (descriptions.Count() > 0)
			{
				strModules += ", \"description\": \"";

				string description = "";
				foreach (var desc in descriptions)
				{
					strModules += desc.ConstructorArguments.FirstOrDefault().Value + ". ";
				}
				strModules += description + "\"";
			}

			strModules += " }, \n";
		}
		return strModules + "]";
	}

	public Type DefaultBuilderType(string module)
	{
		return builderModules.FirstOrDefault(p => p.FullName == module + ".Builder");
	}

	public BaseBuilder GetInstructionBuilderInstance(string module)
	{
		Type classType = GetBuilderType(module) ?? DefaultBuilderType(module); ;

		var classInstance = Activator.CreateInstance(classType) as BaseBuilder;
		if (classInstance == null)
		{
			throw new BuilderException($"Could not create instance of {classType}");
		}
		return classInstance;
	}

	public BaseProgram GetProgramInstance(string module)
	{
		Type? classType = GetRuntimeType(module);

		if (classType == null)
		{
			throw new Exception("Could not find module:" + module);
		}

		var classInstance = Activator.CreateInstance(classType, new Dictionary<string, object>()) as BaseProgram;
		if (classInstance == null)
		{
			throw new Exception($"Could not create instance of {classType}");
		}
		return classInstance;
	}

	public static string GetJsonSchemaForRecord(Type type)
	{
		ConstructorInfo constructor = type.GetConstructors().First();
		var json = "{";
		foreach (var parameter in constructor.GetParameters())
		{
			string value = "";
			if (parameter.HasDefaultValue)
			{
				if (parameter.DefaultValue == null)
				{
					value = "null";
				}
				else
				{
					value = parameter.DefaultValue.ToString()!;
				}
			}
			else
			{
				value = parameter.ParameterType.Name.ToLower().ToString();
			}
			json += "\"" + parameter.Name + "\" : " + value + ", ";
		}
		return json += "}";
	}


	public static string GetJsonSchema(Type type, bool ignoreInstructed = true)
	{
		var json = (type.IsArray || type == typeof(List<>)) ? "[\n" : "{\n";

		var primaryConstructor = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
		var constructorParameters = primaryConstructor?.GetParameters().ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

		foreach (var prop in type.GetProperties())
		{
			if (prop.CustomAttributes.FirstOrDefault(p => p.AttributeType.Name == "JsonIgnoreAttribute") != null) continue;
			if (ignoreInstructed && prop.CustomAttributes.FirstOrDefault(p => p.AttributeType.Name == "IgnoreWhenInstructedAttribute") != null) continue;
			if (prop.CustomAttributes.FirstOrDefault(p => p.AttributeType.Name == "LlmIgnoreAttribute") != null) continue;

			var propName = "\t\"" + prop.Name + "\"";
			if (prop.CustomAttributes.FirstOrDefault(p => p.AttributeType.Name == "NullableAttribute") != null)
			{
				propName += "?";
			}
			if (json.Length > 2) json += ",\n";
			if (prop.PropertyType == typeof(List<string>))
			{
				json += $@"{propName}: string[]";
			}
			else if (prop.PropertyType.FullName.StartsWith("System.Collections.Generic.Dictionary"))
			{
				json += $@"{propName} : {{ {prop.PropertyType.GenericTypeArguments[0].Name} : {prop.PropertyType.GenericTypeArguments[1].Name}, ... }}";
			}
			else if (prop.PropertyType.Name == "Nullable`1")
			{
				json += $@"{propName}?: {prop.PropertyType.GenericTypeArguments[0].Name}";
			}
			else if (prop.PropertyType.IsGenericType)
			{
				var args = prop.PropertyType.GetGenericArguments();
				if (args.Length == 1)
				{
					json += $@"{propName}: [" + GetJsonSchema(args[0]) + "]";
				}
				else if (args.Length == 2)
				{
					json += $@"{propName}: {{ ""key"": {args[0].Name}, ""value"": {args[1].Name} }}";
				}
			}
			else if (prop.PropertyType.IsClass && prop.PropertyType.Namespace.StartsWith("PLang"))
			{
				json += $@"{propName}: " + GetJsonSchema(prop.PropertyType);
				/*
				json += $@"""{prop.Name}:"" {{";
				var properties = prop.PropertyType.GetProperties();
				for (int i=0;i<properties.Length;i++)
				{
					if (i != 0) json += ", ";
					json += $@"""{properties[i].Name}"": {properties[i].PropertyType.Name}";
				}
				json += "}";*/
			}
			else if (prop.PropertyType.IsEnum)
			{
				json += $@"{propName}: enum";
				//prop.PropertyType.GetFields();
			}
			else if (prop.PropertyType.Namespace == type.Namespace)
			{
				json += $@"{propName}: " + GetJsonSchema(prop.PropertyType);
			}
			else
			{
				json += $@"{propName}: {prop.PropertyType.Name.ToLower()}";
			}
			var attribute = prop.GetCustomAttribute<System.ComponentModel.DefaultValueAttribute>();
			if (attribute != null)
			{
				//schema[prop.Name] = " = " + ((DefaultValueAttribute) attribute).Value;
				json += " = " + ((attribute.Value == null) ? "null" : attribute.Value);
			}
			else if (constructorParameters != null && constructorParameters.ContainsKey(prop.Name))
			{
				var item = constructorParameters[prop.Name];
				if (item.HasDefaultValue)
				{
					json += " = " + ((item.DefaultValue == null) ? "null" : item.DefaultValue);
				}
			}
			else if (prop.CustomAttributes.FirstOrDefault(p => p.AttributeType.Name == "NullableAttribute") != null)
			{
				//json += " = null";
			}

			var description = prop.CustomAttributes.FirstOrDefault(p => p.AttributeType.Name == "DescriptionAttribute");
			if (description != null)
			{
				json += " // " + description.ConstructorArguments[0].Value;
			}



		}
		json += (type.IsArray || type == typeof(List<>)) ? "\n]" : "\n}";

		return json;
	}

	public Type? GetBuilderType(string module)
	{
		if (!module.EndsWith(".Builder")) module += ".Builder";
		return builderModules.FirstOrDefault(p => p.FullName == module);
	}


	public async Task<object?> Run(string ns, string cls, string method,
		Dictionary<string, object?>? named = null)
	{
		var type = Type.GetType($"{ns}.{cls}", throwOnError: true)!;
		var mi = type.GetMethod(method, BindingFlags.Public |
											BindingFlags.Instance |
											BindingFlags.Static)
					?? throw new MissingMethodException(type.FullName, method);

		// build positional array once
		var pars = mi.GetParameters();
		var args = pars.Length == 0
			? Array.Empty<object?>()
			: pars.Select(p =>
				  named != null && named.TryGetValue(p.Name!, out var v)
					  ? v
					  : p.HasDefaultValue ? p.DefaultValue
					  : throw new ArgumentException($"Missing '{p.Name}'."))
				  .ToArray();

		var target = mi.IsStatic ? null : Activator.CreateInstance(type);
		return mi.FastInvoke(target, args);   

		//InvokeMethoed(GetProgramInstance(), @namespace, @class, method, Parameters);
	}


	public Type? GetRuntimeType(string? module)
	{
		if (module == null) return null;
		if (!module.EndsWith(".Program")) module += ".Program";
		return runtimeModules.FirstOrDefault(p => p.FullName == module);
	}

	public List<Type> GetBuilderModules()
	{
		return builderModules;
	}
	public List<Type> GetRuntimeModules()
	{
		return runtimeModules;
	}


	public static List<string> GetStaticFields(Type type, BindingFlags bindingFlags = BindingFlags.Static | BindingFlags.Public)
	{
		List<string> keywords = new List<string>();

		FieldInfo[] fields = type.GetFields(bindingFlags);

		foreach (var field in fields)
		{
			if (field.FieldType == typeof(string))
			{
				keywords.Add(field.GetValue(null)!.ToString()!);
			}
		}

		return keywords;

	}

	public static bool IsConsideredPrimitive(Type type)
	{
		if (type.IsArray)
		{
			return true;
		}

		return type.IsPrimitive ||
			   type == typeof(string) ||
			   type.FullName == "System.Object" ||
			   type == typeof(DateTime) ||
			   type == typeof(Guid) ||
			   type == typeof(TimeSpan) ||
			   type == typeof(Uri) ||
			   type == typeof(decimal) ||
			   type == typeof(BigInteger) ||
			   type == typeof(Version) ||
			   type == typeof(JToken);
	}
	public static T? ConvertToType<T>(object? value)
	{
		if (value == null) return default;

		var obj = ConvertToType(value, typeof(T));
		if (obj == null) return default;
		return (T?)obj;
	}
	public static object? ConvertToType(object? value, Type targetType)
	{
		if (value == null) return null;

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

		if (value is JObject jobj)
		{
			return jobj.ToObject(targetType);
		}

		if (value is JArray jArray)
		{
			return jArray.ToObject(targetType);
		}
		if (value is JToken jToken)
		{
			return jToken.ToObject(targetType);
		}

		if (IsListOfJToken(value) && targetType == typeof(string))
		{
			var jArray2 = new JArray((IEnumerable<JToken>)value);
			return jArray2.ToString();
		}


		string? strValue = value.ToString()?.Trim();
		if (strValue != null)
		{
			try
			{
				if (targetType.Name.StartsWith("Nullable"))
				{
					targetType = targetType.GenericTypeArguments[0];
				}
				if (targetType.Name == "XmlDocument")
				{
					XmlDocument doc = new XmlDocument();
					doc.LoadXml(strValue);
					return doc;
				}

				if (targetType == typeof(bool) && IsBoolValue(strValue, out bool? boolValue))
				{
					return boolValue;
				}

				var parseMethod = targetType.GetMethod("Parse", new[] { typeof(string), typeof(CultureInfo) });
				if (parseMethod != null)
				{
					return parseMethod.Invoke(null, new object[] { strValue, CultureInfo.InvariantCulture });
				}
			}
			catch { }
		}

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
					ObjectCreationHandling = ObjectCreationHandling.Replace
				};

				var json = JsonConvert.SerializeObject(value);

				return JsonConvert.DeserializeObject(json, targetType);
			}
			catch
			{
				if (targetType.Name == "String")
				{
					return StringHelper.ConvertToString(value);
				}
				return value;
			}

		}
	}
	static bool IsListOfJToken(object obj)
	{
		if (obj == null) return false;

		var type = obj.GetType();

		if (!type.IsGenericType) return false;

		var genericTypeDef = type.GetGenericTypeDefinition();
		if (genericTypeDef != typeof(List<>)) return false;

		var argType = type.GetGenericArguments()[0];
		return typeof(Newtonsoft.Json.Linq.JToken).IsAssignableFrom(argType);
	}
	public static bool IsBoolValue(string strValue, out bool? boolValue)
	{
		if (strValue == "1" || strValue.Equals("true", StringComparison.OrdinalIgnoreCase))
		{
			boolValue = true;
			return true;
		}
		if (strValue == "0" || strValue.Equals("false", StringComparison.OrdinalIgnoreCase))
		{
			boolValue = false;
			return true;
		}
		boolValue = null;
		return false;
	}

	public static (JToken first, JToken second) ToMatchingJTokens(object a, object b)
		=> (ToToken(a), ToToken(b));

	private static JToken ToToken(object o) => o switch
	{
		null => JValue.CreateNull(),
		JToken jt => jt,
		_ => JToken.FromObject(o)
	};


	public static (object? item1, object? item2) TryConvertToMatchingType(object? item1, object? item2)
	{
		if (item1 == null || item2 == null) return (item1, item2);

		var type1 = item1.GetType();
		var type2 = item2.GetType();

		var numericTypes = new[]
		{
			typeof(byte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
			typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal)
		};



		if (!numericTypes.Contains(type1) || !numericTypes.Contains(type2))
		{
			if (numericTypes.Contains(type1) || numericTypes.Contains(type2))
			{
				(item1, item2) = TryConvertToNumeric(item1, item2, numericTypes, out bool success);
				if (success) return (item1, item2);
			}

			return ToMatchingJTokens(item1, item2);

		}


		Type widerType = GetWiderType(type1, type2);

		var converted1 = Convert.ChangeType(item1, widerType);
		var converted2 = Convert.ChangeType(item2, widerType);

		return (converted1, converted2);
	}

	private static (object item1, object item2) TryConvertToNumeric(object item1, object item2, Type[] numericTypes, out bool success)
	{
		Type theNumericType;
		object toConvert;
		object sameObjToReturn;
		if (numericTypes.Contains(item1.GetType()))
		{
			theNumericType = item1.GetType();
			var obj = TypeHelper.ConvertToType(item2, item1.GetType());
			if (obj != null)
			{
				success = true;
				return (item1, obj);
			}

		}
		else if (numericTypes.Contains(item2.GetType()))
		{
			var obj = TypeHelper.ConvertToType(item1, item2.GetType());
			if (obj != null)
			{
				success = true;
				return (obj, item2);
			}
		}


		success = false;
		return (item1, item2);

	}

	private static Type GetWiderType(Type t1, Type t2)
	{
		var rank = new Dictionary<Type, int>
		{
			[typeof(byte)] = 1,
			[typeof(short)] = 2,
			[typeof(ushort)] = 3,
			[typeof(int)] = 4,
			[typeof(uint)] = 5,
			[typeof(long)] = 6,
			[typeof(ulong)] = 7,
			[typeof(float)] = 8,
			[typeof(double)] = 9,
			[typeof(decimal)] = 10
		};

		return rank[t1] >= rank[t2] ? t1 : t2;
	}


	public static (string?, IBuilderError?) GetMethodAsJson(Type type, string methodName)
	{
		var method = type.GetMethods().FirstOrDefault(m => m.Name == methodName);
		if (method == null)
			return (null, new BuilderError($"Method {methodName} not found in type {type.FullName}."));

		var parameters = method.GetParameters();
		var nl = Environment.NewLine;

		string json = $@"{{{nl}""MethodName"": ""{methodName}"",{nl}""Parameters"": {nl}[";
		foreach (var prop in parameters)
		{
			//var defaultValue = Activator.CreateInstance();
			if (prop.CustomAttributes.FirstOrDefault(p => p.AttributeType.Name == "JsonIgnoreAttribute") !=
				null) continue;
			if (prop.CustomAttributes.FirstOrDefault(p => p.AttributeType.Name == "IgnoreWhenInstructedAttribute") != null) continue;
			if (prop.CustomAttributes.FirstOrDefault(p => p.AttributeType.Name == "LlmIgnoreAttribute") != null) continue;

			json += $@"{{""Type"": ""{prop.ParameterType.ToString()}""\n""Name""";
			var propName = "\t\"" + prop.Name + "\"";
			if (prop.CustomAttributes.FirstOrDefault(p => p.AttributeType.Name == "NullableAttribute") != null)
			{
				propName += "?";
			}

			if (json.Length > 2) json += ",\n";
			if (prop.ParameterType == typeof(List<string>))
			{
				json += $@"{propName}: string[]";
			}
			else if (prop.ParameterType.FullName.StartsWith("System.Collections.Generic.Dictionary"))
			{
				json +=
					$@"{propName} : {{ {prop.ParameterType.GenericTypeArguments[0].Name} : {prop.ParameterType.GenericTypeArguments[1].Name}, ... }}";
			}
			else if (prop.ParameterType.Name == "Nullable`1")
			{
				json += $@"{propName}?: {prop.ParameterType.GenericTypeArguments[0].Name}";
			}
			else if (prop.ParameterType.IsGenericType)
			{
				var args = prop.ParameterType.GetGenericArguments();
				if (args.Length == 1)
				{
					json += $@"{propName}: [" + GetJsonSchema(args[0]) + "]";
				}
				else if (args.Length == 2)
				{
					json += $@"{propName}: {{ ""key"": {args[0].Name}, ""value"": {args[1].Name} }}";
				}
			}
			else if (prop.ParameterType.IsClass && prop.ParameterType.Namespace.StartsWith("PLang"))
			{
				json += $@"{propName}: " + GetJsonSchema(prop.ParameterType);
				/*
				json += $@"""{prop.Name}:"" {{";
				var properties = prop.PropertyType.GetProperties();
				for (int i=0;i<properties.Length;i++)
				{
					if (i != 0) json += ", ";
					json += $@"""{properties[i].Name}"": {properties[i].PropertyType.Name}";
				}
				json += "}";*/
			}
			else if (prop.ParameterType.IsEnum)
			{
				json += $@"{propName}: enum";
				//prop.PropertyType.GetFields();
			}
			else if (prop.ParameterType.Namespace == type.Namespace)
			{
				json += $@"{propName}: " + GetJsonSchema(prop.ParameterType);
			}
			else
			{
				json += $@"{propName}: {prop.ParameterType.Name.ToLower()}";
			}

			var attribute = prop.GetCustomAttribute<System.ComponentModel.DefaultValueAttribute>();
			if (attribute != null)
			{
				//schema[prop.Name] = " = " + ((DefaultValueAttribute) attribute).Value;
				json += " = " + ((attribute.Value == null) ? "null" : attribute.Value);
			}
			/*
			else if (constructorParameters != null && constructorParameters.ContainsKey(prop.Name))
			{
				var item = constructorParameters[prop.Name];
				if (item.HasDefaultValue)
				{
					json += " = " + ((item.DefaultValue == null) ? "null" : item.DefaultValue);
				}
			}*/
			else if (prop.CustomAttributes.FirstOrDefault(p => p.AttributeType.Name == "NullableAttribute") != null)
			{
				//json += " = null";
			}

			var description =
				prop.CustomAttributes.FirstOrDefault(p => p.AttributeType.Name == "DescriptionAttribute");
			if (description != null)
			{
				json += " // " + description.ConstructorArguments[0].Value;
			}
		}

		string returnType = method.ReturnType.ToString();
		return (json + @$"],\n""ReturnType"":{returnType}\n}}", null);
	}
	public static bool IsRecordOrAnonymousType(object obj)
	{
		var type = obj.GetType();
		var result = IsRecordType(type);
		if (result) return result;

		return (type.Name.StartsWith("<>f__Anonymous"));
	}
	public static bool IsRecordType(object obj)
	{
		return IsRecordType(obj.GetType());
	}
	public static bool IsRecordType(Type type)
	{
		var hasCloneMethod = type.GetMethod("<Clone>$") != null;
		var hasPrintMembers = type.GetMethod("PrintMembers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) != null;

		var hasEqualityContract = type.GetProperty("EqualityContract", BindingFlags.Instance | BindingFlags.NonPublic) != null;

		return hasPrintMembers && hasCloneMethod && hasEqualityContract;
	}

	public static bool IsRecordWithToString(object obj)
	{
		var type = obj.GetType();
		bool isRecord = type.GetMethod("PrintMembers", BindingFlags.Instance | BindingFlags.NonPublic) != null;
		if (!isRecord) return false;
		var toStringMethod = obj.GetType().GetMethods().Any(p => p.Name == "ToString" && p.DeclaringType != typeof(object) && p?.DeclaringType != typeof(ValueType)
			&& p?.CustomAttributes.FirstOrDefault(p => p.AttributeType == typeof(CompilerGeneratedAttribute)) == null);

		return toStringMethod;
	}

	public static bool IsListOrDict(object? obj)
	{
		return (obj is IList || obj is IDictionary || obj is ITuple);
	}

	public static bool ImplementsDict(object? obj, out IDictionary? dict)
	{
		var isDict = (obj.GetType().GetInterfaces().FirstOrDefault(p => p.Name.Equals("IDictionary`2")) != null);
		if (!isDict)
		{
			dict = null;
			return false;
		}

		dict = obj as IDictionary;
		if (dict != null) return true;

		if (obj is ExpandoObject eo)
		{
			dict = eo.ToDictionary();
			return true;
		}
		if (obj is IDynamicMetaObjectProvider mop)
		{
			dict = (IDictionary)AsDictionary(obj);
			return true;
		}

		dict = null;
		return false;
	}

	public static IDictionary<object, object>? AsDictionary(object? obj)
	{
		if (obj == null) return null;

		var type = obj.GetType();
		var dictInterface = type.GetInterfaces()
			.FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));

		if (dictInterface == null)
			return null;

		var keyProp = dictInterface.GetProperty("Keys");
		var valueIndexer = dictInterface.GetProperty("Item");

		var keys = (IEnumerable)keyProp.GetValue(obj);
		var result = new Dictionary<object, object>();

		foreach (var key in keys)
			result[key] = valueIndexer.GetValue(obj, new[] { key });

		return result;
	}



	public static string? GetAsString(object? obj, string format = "text")
	{
		if (obj == null) return null;
		if (obj is string str) return str;
		if (obj.GetType().IsPrimitive) return obj.ToString();

		if (obj is JValue || obj is JObject || obj is JArray)
		{
			return obj.ToString();
		}
		if (obj is IError)
		{
			return ((IError)obj).ToFormat(format).ToString();
		}
		else
		{
			string content = obj.ToString()!;
			if (!JsonHelper.IsJson(content))
			{
				content = JsonConvert.SerializeObject(obj);
			}

			return content;
		}
	}

}

