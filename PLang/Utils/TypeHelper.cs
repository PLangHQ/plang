using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Modules;
using System.Data;
using System.Numerics;
using System.Reflection;
using Websocket.Client.Logging;
using static PLang.Modules.DbModule.Program;

namespace PLang.Utils
{
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
		Task Run(string @namespace, string @class, string method, Dictionary<string, object?> parameters);
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
		/*
		public (ClassDescription?, IBuilderError?) GetMethodDescriptions(Type? type, string? methodName = null)
		{

			if (type == null) return (new(), new BuilderError("Type is null"));

			var methods = type.GetMethods().Where(p => p.DeclaringType?.Name == "Program");
			if (methodName != null)
			{
				methods = type.GetMethods().Where(p => p.Name == methodName);
			}
			ClassDescription classDescription = new();
			GroupedBuildErrors errors = new GroupedBuildErrors();

			foreach (var method in methods)
			{
				if (!method.ReturnType.Name.Contains("Task"))
				{
					continue;
				}

				var (desc, supportiveObjects, error) = TypeHelper.GetMethodDescription(type, method.Name);
				if (error != null) errors.Add(error);

				if (desc != null)
				{
					classDescription.Methods.Add(desc);
				}

				if (supportiveObjects?.Count > 0)
				{
					foreach (var so in supportiveObjects)
					{
						var found = classDescription.SupportingObjects.FirstOrDefault(p => p.Type == so.Type);
						if (found == null)
						{
							classDescription.SupportingObjects.Add(so);
						}
					}
				}

			}

			return (classDescription, (errors.Count > 0) ? errors : null);
		}*/

		public string GetMethodsAsString_depcricated(Type type, string? methodName = null)
		{
			if (type == null) return "";


			var methods = type.GetMethods().Where(p => p.DeclaringType.Name == "Program");
			if (methodName != null)
			{
				methods = type.GetMethods().Where(p => p.Name == methodName);
			}
			List<string> methodDescs = new List<string>();

			foreach (var method in methods)
			{
				var strMethod = "";
				if (method.Module.Name != type.Module.Name) continue;
				if (method.Name == "Run" || method.Name == "Dispose" || method.IsSpecialName) continue;

				strMethod += method.Name + "(";
				var parameters = method.GetParameters();
				foreach (var param in parameters)
				{
					if (!strMethod.EndsWith("(")) strMethod += ", ";
					strMethod += param.ParameterType.Name;
					if (param.ParameterType.GenericTypeArguments.Length > 0)
					{
						strMethod += "<";
						for (int i = 0; i < param.ParameterType.GenericTypeArguments.Length; i++)
						{
							if (i != 0) strMethod += ", ";
							strMethod += param.ParameterType.GenericTypeArguments[i].Name;
						}
						strMethod += ">";
					}
					if (param.IsOptional)
					{
						strMethod += "?";
					}

					strMethod += " " + param.Name;
					if (!string.IsNullOrEmpty(param.DefaultValue?.ToString()))
					{
						if (param.DefaultValue.GetType() == typeof(string))
						{
							strMethod += " = \"" + param.DefaultValue + "\"";
						}
						else
						{
							strMethod += " = " + param.DefaultValue;
						}
					}

					var paramDescs = param.CustomAttributes.Where(p => p.AttributeType.Name == "DescriptionAttribute");
					foreach (var desc in paramDescs)
					{
						strMethod += " /* " + desc.ConstructorArguments.FirstOrDefault().Value + " */ ";
					}

				}
				strMethod += ") ";

				if (method.ReturnType == typeof(Task))
				{
					strMethod += " : void";
				}
				else if (method.ReturnType.GenericTypeArguments.Length > 0)
				{
					string returns = "void";

					foreach (var returnType in method.ReturnType.GenericTypeArguments)
					{

						if (returnType.Name.StartsWith("ValueTuple"))
						{
							foreach (var tupleType in returnType.GenericTypeArguments)
							{
								if (tupleType.Name == "Object" || !tupleType.IsAssignableFrom(typeof(IError)))
								{
									if (tupleType.Name.StartsWith("List"))
									{
										returns = $"List<{tupleType.GenericTypeArguments[0].Name}>";
									}
									else if (tupleType.Name.StartsWith("Dictionary"))
									{
										returns = $"Dicionary<{tupleType.GenericTypeArguments[0].Name}, {tupleType.GenericTypeArguments[1].Name}>";

									}
									else
									{
										returns = tupleType.Name;
									}
								}
							}
						}
						else if (!returnType.IsAssignableFrom(typeof(IError)))
						{
							returns = returnType.Name;
						}
					}
					strMethod += $" : {returns} ";
				}
				else
				{
					Console.WriteLine($"WARNING return type of {method.Name} is not Task");
				}

				var descriptions = method.CustomAttributes.Where(p => p.AttributeType.Name == "DescriptionAttribute");
				foreach (var desc in descriptions)
				{
					if (!strMethod.Contains(" // ")) strMethod += " // ";

					strMethod += desc.ConstructorArguments.FirstOrDefault().Value + ". ";
				}

				strMethod += "\n";
				methodDescs.Add(strMethod);
			}



			return string.Join("", methodDescs);
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
		public async Task Run(string @namespace, string @class, string method, Dictionary<string, object?>? Parameters)
		{
			
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
				return value;
			}
		}

		/*

		public (MethodDescription? MethodDescription, List<ComplexDescription>? SuppportiveObjects, IBuilderError? Error) GetMethodDescription(Type type, string methodName)
		{
			var method = type.GetMethods().FirstOrDefault(m => m.Name == methodName);
			if (method == null)
				return (null, null, new BuilderError($"Method {methodName} not found in type {type.FullName}."));

			string? methodDescription = null;
			var descAttribute =
				method.CustomAttributes.FirstOrDefault(p => p.AttributeType.Name == "DescriptionAttribute");
			if (descAttribute != null)
			{
				methodDescription = descAttribute.ConstructorArguments[0].Value as string;
			}

			var parameters = method.GetParameters();

			var paramsDesc = GetParameterDescriptions(parameters);
			if (paramsDesc.Error != null) return (null, null, paramsDesc.Error);

			var returnValueInfo = GetReturnValue(method);

			var md = new MethodDescription()
			{
				Description = methodDescription,
				MethodName = method.Name,
				Parameters = paramsDesc.ParameterDescriptions!,
				ReturnValue = returnValueInfo,
			};
			return (md, paramsDesc.SupportiveObjects, null);
		}
		
		private static ReturnValue? GetReturnValue(MethodInfo method)
		{
			if (method.ReturnType.GenericTypeArguments.Length > 0)
			{
				if (method.ReturnType.GenericTypeArguments[0].Name.StartsWith("Tuple"))
				{
					var type = method.ReturnType.GenericTypeArguments[0].GenericTypeArguments
						.FirstOrDefault(p => !typeof(IError).IsAssignableFrom(p));
					if (type != null && type.FullName != null)
					{
						return new ReturnValue()
						{
							Type = type.FullName
						};
					}
				}
				else
				{
					var returnValueType = GetTypeToUse(method.ReturnType);
					if (returnValueType == "void") return null;

					return new ReturnValue()
					{
						Type = returnValueType
					};

				}
			}

			return null;
		}
		
		private static string GetTypeToUse(Type type)
		{

			Type? typeToUse;
			if (type.GenericTypeArguments[0].GenericTypeArguments.Length > 0)
			{
				typeToUse = type.GenericTypeArguments[0].GenericTypeArguments
						.FirstOrDefault(p => !typeof(IError).IsAssignableFrom(p));

			}
			else
			{
				typeToUse = type.GenericTypeArguments.FirstOrDefault(p => !typeof(IError).IsAssignableFrom(p));
			}

			if (typeToUse == null) return "void";

			var typeToUseString = "void";
			if (typeToUse.Name.Contains("`"))
			{
				string className = typeToUse.Name.Substring(0, typeToUse.Name.IndexOf("`")) + "<";
				var types = typeToUse.GetGenericArguments();
				for (int b = 0; b < types.Length; b++)
				{
					if (b != 0) className += ",";
					className += types[b].Name;
				}
				typeToUseString = className + ">";
			}
			else
			{
				typeToUseString = typeToUse?.FullName;
			}

			if (typeToUseString != null && typeToUseString.Contains("Version="))
			{
				throw new Exception("Should not happend, FullName with version.");
			}


			return typeToUseString ?? "void";
		}
		/*
		public static object? GetParameterInfoDefaultValue(System.Reflection.ParameterInfo parameterInfo)
		{
			if (parameterInfo.HasDefaultValue) return parameterInfo.DefaultValue;

			var attribute = parameterInfo.GetCustomAttribute<System.ComponentModel.DefaultValueAttribute>();
			if (attribute != null)
			{
				return attribute.Value ?? "null";
			}

			if (!parameterInfo.HasDefaultValue && parameterInfo.ParameterType.ToString() != "System.Object")
			{
				var constructors = parameterInfo.ParameterType.GetConstructors();
				constructors = constructors.Where(c => c.GetParameters().Length == 0).ToArray();
				if (constructors.Length > 0)
				{
					return Activator.CreateInstance(parameterInfo.ParameterType);
				}
			}

			return null;
		}*/
		/*
		private (List<IPropertyDescription>? ParameterDescriptions, IBuilderError? Error) GetParameterDescriptions(System.Reflection.ParameterInfo[] parameters)
		{

			List<ComplexDescription> supportiveObjects = new();
			List<IPropertyDescription> parametersDescriptions = new();
			foreach (var parameterInfo in parameters)
			{
				if (parameterInfo.CustomAttributes.FirstOrDefault(p => p.AttributeType.Name == "JsonIgnoreAttribute") !=
					null) continue;
				if (parameterInfo.CustomAttributes.FirstOrDefault(p => p.AttributeType.Name == "IgnoreWhenInstructedAttribute") != null) continue;
				if (parameterInfo.CustomAttributes.FirstOrDefault(p => p.AttributeType.Name == "LlmIgnoreAttribute") != null) continue;

				if (string.IsNullOrWhiteSpace(parameterInfo.Name))
				{
					return (null, null, new BuilderError($"Parameter '{parameterInfo.Name}' has no name."));
				}

				object? defaultValue = GetParameterInfoDefaultValue(parameterInfo);
				
				bool isRequired = parameterInfo.GetCustomAttribute(typeof(System.Runtime.InteropServices.OptionalAttribute)) == null;
				IPropertyDescription pd;
				if (IsConsideredPrimitive(parameterInfo.ParameterType))
				{
					pd = new PrimitiveDescription
					{
						Type = parameterInfo.ParameterType.ToString(),
						Name = parameterInfo.Name,
						DefaultValue = defaultValue,
						IsRequired = isRequired
					};
				}
				else if (parameterInfo.ParameterType.IsEnum)
				{
					var enums = Enum.GetNames(parameterInfo.ParameterType);
					var defaultEnum = (defaultValue != null) ? Enum.Parse(parameterInfo.ParameterType, defaultValue.ToString()) : defaultValue;
					pd = new EnumDescription()
					{
						Type = parameterInfo.ParameterType.ToString(),
						Name = parameterInfo.Name,
						AvailableValues = string.Join("|", enums),
						DefaultValue = defaultEnum,
						IsRequired = isRequired
					};
				}
				else
				{
					Type item = parameterInfo.ParameterType;
					if (parameterInfo.ParameterType.Name == "List`1")
					{
						item = parameterInfo.ParameterType.GenericTypeArguments[0];
					}


					object? instance = null;
					if (item.ToString() != "System.Object")
					{
						var constructors = item.GetConstructors();
						constructors = constructors.Where(c => c.GetParameters().Length == 0).ToArray();
						if (constructors.Length > 0)
						{
							instance = Activator.CreateInstance(item);
						}
					}

					pd = new ComplexDescription()
					{
						Type = parameterInfo.ParameterType.ToString(),
						Name = parameterInfo.Name,
						TypeProperties = GetPropertyInfos(item.GetProperties(), instance, item),
						IsRequired = isRequired
					};

					supportiveObjects.Add(pd as ComplexDescription);
					pd = new ComplexDescription()
					{
						Type = parameterInfo.ParameterType.ToString() + " (see SupportingObjects)",
						Name = parameterInfo.Name,
						IsRequired = isRequired
					};
				}


				if (parameterInfo.CustomAttributes.FirstOrDefault(p =>
						p.AttributeType.Name is "NullableAttribute" or "OptionalAttribute") != null)
				{
					//pd.Defa = false;
				}


				if (parameterInfo.ParameterType == typeof(List<string>))
				{
					pd.Type = "List<string>";
				}
				else if (parameterInfo.ParameterType.FullName != null &&
						 parameterInfo.ParameterType.FullName.StartsWith("System.Collections.Generic.Dictionary"))
				{
					pd.Type =
						$"Dictionary<{parameterInfo.ParameterType.GenericTypeArguments[0].Name}, {parameterInfo.ParameterType.GenericTypeArguments[1].Name}>";
				}
				else if (parameterInfo.ParameterType.Name == "Nullable`1")
				{
					//pd.IsRequired = false;
				}



				var description =
					parameterInfo.CustomAttributes.FirstOrDefault(p => p.AttributeType.Name == "DescriptionAttribute");
				if (description != null)
				{
					pd.Description += description.ConstructorArguments[0].Value;
				}

				parametersDescriptions.Add(pd);
			}

			return (parametersDescriptions, supportiveObjects, null);
		}

		private List<IPropertyDescription>? GetPropertyInfos(PropertyInfo[] properties, object? instance, Type item, int depth = 0)
		{
			if (depth == 10)
			{
				return null;
			}

			List<IPropertyDescription> parameterDescriptions = new();
			foreach (var propertyInfo in properties)
			{


				if (!propertyInfo.CanWrite || propertyInfo.CustomAttributes.FirstOrDefault(p => p.AttributeType.Name == "JsonIgnoreAttribute") !=
					null) continue;
				if (propertyInfo.CustomAttributes.FirstOrDefault(p => p.AttributeType.Name == "IgnoreWhenInstructedAttribute") != null) continue;
				if (propertyInfo.CustomAttributes.FirstOrDefault(p => p.AttributeType.Name == "LlmIgnoreAttribute") != null) continue;

				var propertyType = propertyInfo.PropertyType;
				if (propertyInfo.PropertyType.Name.StartsWith("Nullable`1"))
				{
					propertyType = propertyInfo.PropertyType.GenericTypeArguments[0];
				}
				bool isRequired = propertyInfo.GetCustomAttribute(typeof(System.Runtime.InteropServices.OptionalAttribute)) == null;
				object? defaultValue = null;
				if (instance != null)
				{
					try
					{
						defaultValue = instance.GetType().GetProperty(propertyType.Name)?.GetValue(instance);
					}
					catch
					{
						// ignored
					}
				}

				IPropertyDescription pd;
				if (IsConsideredPrimitive(propertyType))
				{
					pd = new PrimitiveDescription
					{
						Type = propertyInfo.PropertyType.ToString(),
						Name = propertyInfo.Name,
						DefaultValue = defaultValue,
						IsRequired = isRequired
					};
				}
				else if (propertyType.IsEnum)
				{
					var enums = Enum.GetNames(propertyType);
					var defaultEnum = (defaultValue != null) ? Enum.Parse(propertyType, defaultValue.ToString()) : defaultValue;
					pd = new EnumDescription()
					{
						Type = propertyInfo.PropertyType.ToString(),
						Name = propertyInfo.Name,
						AvailableValues = string.Join("|", enums),
						DefaultValue = defaultEnum,
						IsRequired = isRequired
					};
				}
				else
				{
					object? instance2 = null;
					if (propertyType.ToString() != "System.Object")
					{
						var constructors = propertyType.GetConstructors();
						constructors = constructors.Where(c => c.GetParameters().Length == 0).ToArray();
						if (constructors.Length > 0)
						{
							instance2 = Activator.CreateInstance(propertyType);
						}
					}
					if (item == propertyType)
					{
						pd = new ComplexDescription()
						{
							Type = propertyType.ToString(),
							Name = propertyInfo.Name,
							TypeProperties = [],
							IsRequired = isRequired
						};
					}
					else
					{

						pd = new ComplexDescription()
						{
							Type = propertyType.ToString(),
							Name = propertyType.Name,
							TypeProperties = GetPropertyInfos(item.GetProperties(), instance, item),
							IsRequired = isRequired
						};

						supportiveObjects.Add(pd as ComplexDescription);
						pd = new ComplexDescription()
						{
							Type = propertyType.ToString() + " (see SupportingObjects)",
							Name = propertyType.Name,
							IsRequired = isRequired
						};
						pd = new ComplexDescription()
						{
							Type = propertyType.ToString(),
							Name = propertyInfo.Name,
							TypeProperties = GetPropertyInfos(propertyType.GetProperties(), instance2, item, ++depth),
							IsRequired = isRequired
						};
					}
				}

				if (propertyInfo.CustomAttributes.FirstOrDefault(p =>
						p.AttributeType.Name is "NullableAttribute" or "OptionalAttribute") != null)
				{
					//  pd.IsRequired = false;
				}


				if (propertyType == typeof(List<string>))
				{
					pd.Type = "List<string>";
				}
				else if (propertyType.FullName != null &&
						 propertyType.FullName.StartsWith("System.Collections.Generic.Dictionary"))
				{
					pd.Type =
						$"Dictionary<{propertyType.GenericTypeArguments[0].Name}, {propertyType.GenericTypeArguments[1].Name}>";
				}

				var description =
					propertyInfo.CustomAttributes.FirstOrDefault(p => p.AttributeType.Name == "DescriptionAttribute");
				if (description != null)
				{
					pd.Description += description.ConstructorArguments[0].Value;
				}

				parameterDescriptions.Add(pd);
			}

			return (parameterDescriptions.Count > 0) ? parameterDescriptions : null;
		}
		*/
		/*
		public static bool IsConsideredPrimitive(Type type)
		{
			if (type.IsArray)
			{
				return true;
			}

			return type.IsPrimitive ||
				   type == typeof(string) ||
				   type == typeof(DateTime) ||
				   type == typeof(Guid) ||
				   type == typeof(TimeSpan) ||
				   type == typeof(Uri) ||
				   type == typeof(decimal) ||
				   type == typeof(BigInteger) ||
				   type == typeof(Version) ||
				   type == typeof(JToken);
		}
		*/

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
	}
}
