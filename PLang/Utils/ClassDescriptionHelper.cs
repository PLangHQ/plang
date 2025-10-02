using Castle.Components.DictionaryAdapter;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Nethereum.Contracts.Standards.ERC20.TokenList;
using Nethereum.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Container;
using PLang.Errors;
using PLang.Errors.Builder;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Utils
{
	public class ClassDescriptionHelper
	{

		ClassDescription classDescription = new();
		public ClassDescriptionHelper()
		{
		}

		public (ClassDescription? ClassDescription, IBuilderError? Error) GetClassDescription(Type type, string? methodName = null)
		{

			if (type == null) return (new(), new BuilderError("Type is null"));

			var methods = type.GetMethods().Where(p => p.DeclaringType?.Name == "Program");
			if (methodName != null)
			{
				methods = type.GetMethods().Where(p => p.Name == methodName);
			}


			GroupedBuildErrors errors = new GroupedBuildErrors();

			foreach (var method in methods)
			{
				if (!method.ReturnType.Name.Contains("Task") || method.Name.Equals("AsyncConstructor"))
				{
					continue;
				}

				var (desc, error) = GetMethodDescription(type, method);
				if (error != null) errors.Add(error);

				if (desc != null)
				{
					classDescription.Methods.Add(desc);
				}


			}

			return (classDescription, (errors.Count > 0) ? errors : null);
		}

		private void AddSupportiveObject(MethodInfo method, ComplexDescription co)
		{
			if (co.Type.StartsWith("Dictionary<") || co.Type.StartsWith("List<") || co.Type.StartsWith("Tuple<")) return;

			var found = classDescription.SupportingObjects.FirstOrDefault(p => p.Type == co.Type);
			if (found == null)
			{
				co.MethodNames.Add(method.Name);
				classDescription.SupportingObjects.Add(co);
			} else
			{
				found.MethodNames.Add(method.Name);
			}
		}
		
		public (MethodDescription? MethodDescription, IBuilderError? Error) GetMethodDescription(Type type, MethodInfo method)
		{
		
			string? methodDescription = null;
			var descAttribute =
				method.CustomAttributes.FirstOrDefault(p => p.AttributeType.Name == "DescriptionAttribute");
			if (descAttribute != null)
			{
				methodDescription = descAttribute.ConstructorArguments[0].Value as string;
			}

			var parameters = method.GetParameters();

			var paramsDesc = GetParameterDescriptions(method, parameters);
			if (paramsDesc.Error != null) return (null, paramsDesc.Error);

			var returnValueInfo = GetReturnValue(method);

			var md = new MethodDescription()
			{
				Description = methodDescription,
				MethodName = method.Name,
				Parameters = paramsDesc.ParameterDescriptions!,
				ReturnValue = returnValueInfo,
			};
			return (md, null);
		}

		private (List<IPropertyDescription>? ParameterDescriptions, IBuilderError? Error) GetParameterDescriptions(MethodInfo method, System.Reflection.ParameterInfo[] parameters)
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
					return (null, new BuilderError($"Parameter '{parameterInfo.Name}' has no name."));
				}
				/*
				if (parameterInfo.ParameterType == typeof(List<object>))
				{
					return (null, new BuilderError($"Parameter '{parameterInfo.Name}' is List<object>. It cannot be a unclear object, it must be a defined class"));
				}*/

				object? defaultValue = GetParameterInfoDefaultValue(parameterInfo);

				bool isRequired = parameterInfo.GetCustomAttribute(typeof(System.Runtime.InteropServices.OptionalAttribute)) == null;
				string? parameterDescription = null;
				var descriptionAttribute =
					parameterInfo.CustomAttributes.FirstOrDefault(p => p.AttributeType.Name == "DescriptionAttribute");
				if (descriptionAttribute != null)
				{
					parameterDescription += string.Join("\n", descriptionAttribute.ConstructorArguments.Select(p => p.Value));
				}
				IPropertyDescription pd;
				if (TypeHelper.IsConsideredPrimitive(parameterInfo.ParameterType))
				{
					pd = new PrimitiveDescription
					{
						Type = parameterInfo.ParameterType.FullNameNormalized(),
						Name = parameterInfo.Name,
						DefaultValue = defaultValue,
						IsRequired = isRequired, 
						Description = parameterDescription
					};
				}
				else if (parameterInfo.ParameterType.IsEnum)
				{
					var enums = Enum.GetNames(parameterInfo.ParameterType);
					var defaultEnum = (defaultValue != null) ? Enum.Parse(parameterInfo.ParameterType, defaultValue.ToString()) : defaultValue;
					pd = new EnumDescription()
					{
						Type = parameterInfo.ParameterType.FullNameNormalized(),
						Name = parameterInfo.Name,
						AvailableValues = string.Join("|", enums),
						DefaultValue = defaultEnum,
						IsRequired = isRequired,
						Description = parameterDescription
					};
				}
				else
				{
					Type item = parameterInfo.ParameterType;
					if (parameterInfo.ParameterType.Name == "List`1")
					{
						item = parameterInfo.ParameterType.GenericTypeArguments[0];
					}

					string? complexObjectDescription = null;
					descriptionAttribute = item.CustomAttributes.FirstOrDefault(p => p.AttributeType.Name == "DescriptionAttribute");
					if (descriptionAttribute != null)
					{
						complexObjectDescription += string.Join("\n", descriptionAttribute.ConstructorArguments.Select(p => p.Value));
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

					var props = item.GetProperties().OrderBy(p => p.MetadataToken).ToArray();
					var cd = new ComplexDescription()
					{
						Type = parameterInfo.ParameterType.FullNameNormalized(),
						Name = parameterInfo.Name,
						TypeProperties = GetPropertyInfos(method, props, instance, item),
						Description = complexObjectDescription
					};
					if (cd.TypeProperties != null)
					{
						AddSupportiveObject(method, cd);

						pd = new ComplexDescription()
						{
							Type = parameterInfo.ParameterType.FullNameNormalized(),
							Name = parameterInfo.Name,
							Description = (parameterDescription + " (see Type information in SupportingObjects)").Trim(),
							IsRequired = isRequired,
							
						};
					}
					else
					{
						pd = cd;
					}
				}


				if (parameterInfo.CustomAttributes.FirstOrDefault(p =>
						p.AttributeType.Name is "NullableAttribute" or "OptionalAttribute") != null)
				{
					//pd.Defa = false;
				}

				/*
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
				}*/

				parametersDescriptions.Add(pd);
			}

			return (parametersDescriptions, null);
		}

		private List<IPropertyDescription>? GetPropertyInfos(MethodInfo method, PropertyInfo[] properties, object? instance, Type item, int depth = 0)
		{
			if (depth == 10)
			{
				return null;
			}

			if (TypeHelper.IsConsideredPrimitive(item))
			{
				return null;
			}

			bool isDictOrList = IsDictOrList(item);
			if (isDictOrList)
			{
				foreach (var genericType in item.GenericTypeArguments)
				{
					var propInfos = GetPropertyInfos(method, genericType.GetProperties(), null, genericType);
					if (propInfos == null) continue;

					foreach (var propInfo in propInfos)
					{
						if (propInfo is ComplexDescription cd)
						{
							AddSupportiveObject(method, cd);
						}
					}
				}

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
				if (isRequired)
				{
					var ctx = new NullabilityInfoContext();
					isRequired = ctx.Create(propertyInfo).ReadState == NullabilityState.NotNull;
				}
				/*
				// --- generated property (same information) ---
				var nameProp = type.GetProperty(nameof(Person.Name))!;
				bool propIsNullable = ctx.Create(nameProp).ReadState == NullabilityState.Nullable;
				*/

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

				string? propertyDescription = null;
				var descriptionAttribute =
					propertyInfo.CustomAttributes.FirstOrDefault(p => p.AttributeType.Name == "DescriptionAttribute");
				if (descriptionAttribute != null)
				{
					propertyDescription = string.Join("\n", descriptionAttribute.ConstructorArguments.Select(p => p.Value));
				}

				IPropertyDescription pd;
				if (TypeHelper.IsConsideredPrimitive(propertyType))
				{
					pd = new PrimitiveDescription
					{
						Type = propertyInfo.PropertyType.FullNameNormalized(),
						Name = propertyInfo.Name,
						DefaultValue = defaultValue,
						IsRequired = isRequired,
						Description = propertyDescription
					};
				}
				else if (propertyType.IsEnum)
				{
					var enums = Enum.GetNames(propertyType);
					var defaultEnum = (defaultValue != null) ? Enum.Parse(propertyType, defaultValue.ToString()) : defaultValue;
					pd = new EnumDescription()
					{
						Type = propertyInfo.PropertyType.FullNameNormalized(),
						Name = propertyInfo.Name,
						AvailableValues = string.Join("|", enums),
						DefaultValue = defaultEnum,
						IsRequired = isRequired,
						Description = propertyDescription
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
						string? complexObjectDescription = GetDescriptionAttribute(item, propertyType,  null);
						pd = new ComplexDescription()
						{
							Type = propertyType.FullNameNormalized(),
							Name = propertyInfo.Name,
							TypeProperties = [],
							IsRequired = isRequired,
							Description = complexObjectDescription
						};
					}
					else
					{
						string? complexObjectDescription = GetDescriptionAttribute(item, propertyType, propertyInfo.Name);
						var props = propertyType.GetProperties().OrderBy(p => p.MetadataToken).ToArray();
						var cd = new ComplexDescription()
						{
							Type = propertyType.FullNameNormalized(),
							Name = propertyInfo.Name,
							TypeProperties = GetPropertyInfos(method, props, instance2, propertyType, ++depth),
							IsRequired = isRequired,
							Description = complexObjectDescription
						};
						if (cd.TypeProperties != null)
						{
							AddSupportiveObject(method, cd);
							pd = new ComplexDescription()
							{
								Type = propertyType.FullNameNormalized(),
								Name = propertyInfo.Name,
								Description = ($" (see {propertyType.FullNameNormalized()} type information in SupportingObjects)").Trim(),
								IsRequired = isRequired
							};
						}
						else
						{
							pd = cd;
						}

					}
				}

				if (propertyInfo.CustomAttributes.FirstOrDefault(p =>
						p.AttributeType.Name is "NullableAttribute" or "OptionalAttribute") != null)
				{
					//  pd.IsRequired = false;
				}

				/*
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
				*/
				

				parameterDescriptions.Add(pd);
			}

			return (parameterDescriptions.Count > 0) ? parameterDescriptions : null;
		}

		private string? GetDescriptionAttribute(Type item, Type propertyType, string? propertyName)
		{
			if (propertyName != null)
			{
				var prop = item.GetProperty(propertyName);
				var attr = prop?.GetCustomAttribute<DescriptionAttribute>();
				if (!string.IsNullOrEmpty(attr?.Description))
				{
					return attr?.Description;
				}
			}

			string? complexObjectDescription = null;
			var descriptionAttribute = item.CustomAttributes.FirstOrDefault(p => p.AttributeType.Name == "DescriptionAttribute");
			if (descriptionAttribute != null)
			{
				complexObjectDescription += string.Join("\n", descriptionAttribute.ConstructorArguments.Select(p => p.Value));
			}
			
			descriptionAttribute = propertyType.CustomAttributes.FirstOrDefault(p => p.AttributeType.Name == "DescriptionAttribute");
			if (descriptionAttribute != null)
			{
				complexObjectDescription += string.Join("\n", descriptionAttribute.ConstructorArguments.Select(p => p.Value));
			}


			return complexObjectDescription;
			
		}

		private bool IsDictOrList(Type type)
		{
			var result = (type.Name.StartsWith("Dictionary`") || type.Name.StartsWith("List`") || type.Name.StartsWith("Tuple`"));
			if (result) return true;

			var baseType = type.BaseType;
			if (baseType == null) return false;

			result = (baseType.Name.StartsWith("Dictionary`") || baseType.Name.StartsWith("List`") || baseType.Name.StartsWith("Tuple`"));
			return result;
		}

		public object? GetParameterInfoDefaultValue(System.Reflection.ParameterInfo parameterInfo)
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
		}
		private ReturnValue? GetReturnValue(MethodInfo method)
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



		private string GetTypeToUse(Type type)
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
	}
}
