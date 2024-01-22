using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Building.Events;
using PLang.Building.Model;

using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Modules;
using PLang.Services.SettingsService;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO.Abstractions;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static Dapper.SqlMapper;

namespace PLang.Utils
{
	public interface ITypeHelper
	{
		BaseBuilder GetInstructionBuilderInstance(string module);
		string GetMethodsAsString(Type type, string? methodName = null);
		List<Type> GetRuntimeModules();
		string GetModulesAsString(List<string>? excludedModules = null);
		BaseProgram GetProgramInstance(string module);
		List<Type> GetBuilderModules();
		Type? GetBuilderType(string module);
		Type? GetRuntimeType(string module);
	}

	public class TypeHelper : ITypeHelper
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly ISettings settings;

		private static List<Type> runtimeModules = new List<Type>();
		private static List<Type> baseRuntimeModules = new List<Type>();
		private static List<Type> builderModules = new List<Type>();
		private static List<Type> baseBuilderModules = new List<Type>();

		public TypeHelper(IPLangFileSystem fileSystem, ISettings settings)
		{
			LoadModules(fileSystem, fileSystem.GoalsPath);
			this.fileSystem = fileSystem;
			this.settings = settings;
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

			// Get all types in the assembly
			Type[] types2 = executingAssembly.GetTypes();

			// Filter and print types that implement IDbConnection
			foreach (Type type1 in types)
			{
				if (typeof(IDbConnection).IsAssignableFrom(type1) && !type1.IsInterface)
				{
					Console.WriteLine(type.FullName);
				}
			}

			string modulesDirectory = Path.Combine(fileSystem.GoalsPath, "modules");
			if (!fileSystem.Directory.Exists(modulesDirectory)) return types;

			foreach (var dll in fileSystem.Directory.GetFiles(modulesDirectory, "*.dll"))
			{
				// Load the assembly
				Assembly loadedAssembly = Assembly.LoadFile(dll);

				// Get types that implement IProgram from the loaded assembly
				var typesFromAssembly = loadedAssembly.GetTypes().Where(t => t == type).ToList();
				if (typesFromAssembly.Count > 0)
				{
					// Add the found types to the main list
					types.AddRange(typesFromAssembly);
				}
			}
			return types;
		}

		public string GetMethodsAsString(Type type, string? methodName = null)
		{
			if (type == null) return "";

			var methods = type.GetMethods().Where(p => p.DeclaringType.Name == "Program");
			if (methodName != null)
			{
				methods = type.GetMethods().Where(p => p.Name == methodName);
			}
			List<string> methodDescs = new List<string>();
			var strMethods = "";

			foreach (var method in methods)
			{
				var strMethod = "";
				if (method.Module.Name != type.Module.Name) continue;
				if (method.Name == "Run" || method.Name == "Dispose" || method.IsSpecialName) continue;

				if (method.ReturnType == typeof(Task))
				{
					strMethod += "void ";
				}
				else if (method.ReturnType.GenericTypeArguments.Length > 0)
				{
					strMethod += method.ReturnType.GenericTypeArguments[0].Name + " ";
				}
				else
				{
					
					Console.WriteLine($"WARNING return type of {method.Name} is not Task");
				}

				strMethod += method.Name + "(";
				var parameters = method.GetParameters();
				foreach (var param in parameters)
				{
					if (!strMethod.EndsWith("(")) strMethod += ", ";
					strMethod += param.ParameterType.Name;
					if (param.ParameterType.GenericTypeArguments.Length > 0)
					{
						strMethod += "<";
						for (int i=0;i<param.ParameterType.GenericTypeArguments.Length;i++) 
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

				}
				strMethod += ") ";


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

		public string GetModulesAsString(List<string>? excludedModules = null)
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
			foreach (ParameterInfo parameter in constructor.GetParameters())
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
				} else
				{
					value = parameter.ParameterType.Name.ToLower().ToString();
				}
				json += "\"" + parameter.Name + "\" : " + value + ", ";
			}
			return json += "}";
		}

		public static string GetJsonSchema(Type type)
		{
			var json = (type.IsArray || type == typeof(List<>)) ? "[" : "{";

			foreach (var prop in type.GetProperties())
			{
				var propName = "\"" + prop.Name + "\"";
				if (prop.CustomAttributes.FirstOrDefault(p => p.AttributeType.Name == "NullableAttribute") != null)
				{
					propName += "?";
				}
				if (json.Length > 1) json += ",\n";
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
				var attribute = prop.GetCustomAttribute<DefaultValueAttribute>();
				if (attribute != null)
				{
					//schema[prop.Name] = " = " + ((DefaultValueAttribute) attribute).Value;
					json += " = " + attribute.Value;
				}
				else if (prop.CustomAttributes.FirstOrDefault(p => p.AttributeType.Name == "NullableAttribute") != null)
				{
					//json += " = null";
				}

			}
			json += (type.IsArray || type == typeof(List<>)) ? "]" : "}";

			return json;
		}



		public static void LoadModules(IPLangFileSystem fileSystem, string goalPath)
		{
			if (runtimeModules.Count == 0)
			{
				LoadRuntimeModules(fileSystem, goalPath);
			}
			if (builderModules.Count == 0)
			{
				LoadBuilderModules(fileSystem, goalPath);
			}
		}

		private static void LoadBuilderModules(IPLangFileSystem fileSystem, string goalPath)
		{
			var executingAssembly = Assembly.GetExecutingAssembly();

			builderModules = executingAssembly.GetTypes()
				.Where(t => typeof(BaseBuilder).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
			.ToList();
			baseBuilderModules.AddRange(runtimeModules);

			string modulesDirectory = Path.Combine(goalPath, "modules");
			if (!fileSystem.Directory.Exists(modulesDirectory)) return;

			foreach (var dll in fileSystem.Directory.GetFiles(modulesDirectory, "*.dll"))
			{
				// Load the assembly
				Assembly loadedAssembly = Assembly.LoadFile(dll);

				// Get types that implement IProgram from the loaded assembly
				var typesFromAssembly = loadedAssembly.GetTypes()
					.Where(t => typeof(BaseBuilder).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
					.ToList();

				// Add the found types to the main list
				builderModules.AddRange(typesFromAssembly);
			}

		}

		private static void LoadRuntimeModules(IPLangFileSystem fileSystem, string goalPath)
		{
			var executingAssembly = Assembly.GetExecutingAssembly();

			runtimeModules = executingAssembly.GetTypes()
				.Where(t => typeof(BaseProgram).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
			.ToList();
			baseRuntimeModules.AddRange(runtimeModules);

			string modulesDirectory = Path.Combine(goalPath, "modules");
			if (!fileSystem.Directory.Exists(modulesDirectory)) return;

			foreach (var dll in fileSystem.Directory.GetFiles(modulesDirectory, "*.dll"))
			{
				// Load the assembly
				Assembly loadedAssembly = Assembly.LoadFile(dll);

				// Get types that implement IProgram from the loaded assembly
				var typesFromAssembly = loadedAssembly.GetTypes()
					.Where(t => typeof(BaseProgram).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
					.ToList();

				// Add the found types to the main list
				runtimeModules.AddRange(typesFromAssembly);
			}
		}
		public Type? GetBuilderType(string module)
		{
			return builderModules.FirstOrDefault(p => p.FullName == module + ".Builder");
		}

		public Type? GetRuntimeType(string module)
		{
			return runtimeModules.FirstOrDefault(p => p.FullName == module + ".Program");
		}

		public List<Type> GetBuilderModules()
		{
			return builderModules;
		}
		public List<Type> GetRuntimeModules()
		{
			return runtimeModules;
		}
	}
}
