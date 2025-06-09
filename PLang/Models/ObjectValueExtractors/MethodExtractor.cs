using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Asn1.X509.Qualified;
using Org.BouncyCastle.Crypto;
using PLang.Exceptions;
using PLang.Models.ObjectValueConverters;
using PLang.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Models.ObjectValueExtractors
{
	public class MethodExtractor : IExtractor
	{
		private object obj;
		private readonly ObjectValue parent;

		public MethodExtractor(object obj, ObjectValue parent)
		{
			this.obj = obj;
			this.parent = parent;
		}
		public ObjectValue? Extract(PathSegment segment, MemoryStack? memoryStack = null)
		{
			IList? list = null;
			if (obj is JValue) obj = obj.ToString();
			if (obj is IList tmp && tmp.Count > 0)
			{
				list = tmp;
				obj = list[0];
			}
			if (obj == null) return ObjectValue.Null;

			string methodDescription = segment.Value;

			var methodName = methodDescription.Substring(0, methodDescription.IndexOf("("));
			var paramString = methodDescription.Substring(methodName.Length + 1, methodDescription.Length - methodName.Length - 2).TrimEnd(')');

			var splitParams = paramString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
			List<object> paramValues = new List<object>();
			splitParams.ForEach(p => paramValues.Add(p));

			(var methods, obj) = GetMethodsOnType(obj, methodName, paramValues);

			AppContext.TryGetSwitch("Builder", out bool isBuilder);
			if (!methods.Any() && !isBuilder)
			{
				throw new MethodNotFoundException($"Method {methodName} not found on {parent.PathAsVariable}");
			}


			foreach (var method in methods)
			{
				try
				{
					var convertedParams = GetParameters(memoryStack, paramValues, method);
					if (convertedParams == null) continue;
					if (list != null)
					{
						for (int i=0;i<list.Count;i++)
						{
							if (list[i] is JValue && obj is string str)
							{
								object? result = Invoke(method, list[i]?.ToString(), convertedParams);
								list[i] = new JValue(result);
							} else
							{
								list[i] = Invoke(method, list[i], convertedParams);
							}
						}
						return new ObjectValue(segment.Value, list, parent: parent, properties: parent.Properties);
					} else
					{
						var result = Invoke(method, obj, convertedParams);
						return new ObjectValue(segment.Value, result, parent: parent, properties: parent.Properties);
					}

				}
				catch (Exception ex)
				{
					throw;
				}
			}
			throw new NotImplementedException();
		}

		private object? Invoke(MethodInfo method, object? obj, object[] parameters)
		{
			if (obj == null) return null;

			object? result = null;
			if (method.IsStatic)
			{
				result = method.Invoke(null, parameters);
			} else
			{
				result = method.Invoke(obj, parameters);
			}
			if (result is Task task)
			{
				task.GetAwaiter().GetResult();

				var taskType = task.GetType();
				if (taskType.IsGenericType && taskType.GetGenericTypeDefinition() == typeof(Task<>))
				{
					var resultProperty = taskType.GetProperty("Result");
					if (resultProperty != null)
					{
						result = resultProperty.GetValue(obj);
					}
					else
					{
						result = null;
					}
				}
			}

			return result;
		}

		private (IEnumerable<MethodInfo>, object? obj) GetMethodsOnType(object obj, string methodName, List<object> paramValues)
		{
			var type = obj.GetType();
			var methods = type.GetMethods().Where(p => !p.IsStatic && p.Name.ToLower() == methodName.ToLower() && p.GetParameters().Length == paramValues.Count);
			if (methods.Any()) return (methods, obj);

			methods = type.GetMethods().Where(p => p.IsStatic && p.Name.ToLower() == methodName.ToLower() && p.GetParameters().Length == paramValues.Count);
			if (methods.Any())
			{
				paramValues.Insert(0, obj);
				return (methods, obj);
			}

			methods = GetExtensionMethods(type, methodName, obj);
			if (methods != null && methods.Any())
			{
				paramValues.Insert(0, obj);
				return (methods, obj);
			}

			if (obj is JValue)
			{
				obj = obj.ToString() ?? string.Empty; 
				return GetMethodsOnType(obj, methodName, paramValues);
			}


			return (new List<MethodInfo>(), obj);

		}
		private IEnumerable<MethodInfo>? GetExtensionMethods(Type extendedType, string methodName, object obj)
		{
			var type = this.GetType().Assembly.GetTypes().FirstOrDefault(p => p.Name == extendedType.Name + "Extension");
			if (type != null)
			{
				var query = from method in type.GetMethods(BindingFlags.Static | BindingFlags.Public)
							where method.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false)
							where method.GetParameters()[0].ParameterType == extendedType
							where method.Name.ToLower() == methodName.ToLower()
							select method;
				return query;
			}

			if (obj is IEnumerable)
			{
				var query2 = from t in Assembly.GetAssembly(typeof(Enumerable)).GetTypes()
							 where t.IsClass && t.Namespace == "System.Linq"
							 select t;

				// Get all the methods within these types
				var methods = query2.SelectMany(t => t.GetMethods()).Distinct();

				// Filter out the methods that match the given name
				return methods.Where(m => m.Name.ToLower() == methodName.ToLower());
			}
			return null;

		}

		private static object[]? GetParameters(MemoryStack? memoryStack, List<object> paramValues, MethodInfo method)
		{
			var parameters = method.GetParameters();
			object[] convertedParams = new object[parameters.Length];

			for (int i = 0; i < parameters.Length; i++)
			{
				var paramType = parameters[i].ParameterType;
				if (paramType == typeof(string))
				{
					var paramValue = paramValues[i].ToString().Trim()
							.Replace("\\\\", "\\").Replace("\\\"", "\"");
					if (paramValue.StartsWith("\"") && paramValue.EndsWith("\""))
					{
						paramValue = paramValue.Substring(1, paramValue.Length - 2);
					}
					convertedParams[i] = paramValue;
				}
				else if (paramValues[i].GetType() == paramType)
				{
					convertedParams[i] = paramValues[i];
				}
				else if (paramType.IsGenericType && paramType.GetGenericTypeDefinition() == typeof(IEnumerable<>) && paramValues[i] is IEnumerable)
				{
					convertedParams[i] = paramValues[i];
					method = method.MakeGenericMethod(paramValues[i].GetType().GetGenericArguments()[0]);
				}
				else
				{
					string strValue = paramValues[i].ToString() ?? "";
					if (strValue.StartsWith("\"") && strValue.EndsWith("\""))
					{
						if (paramType.Name == "Char" && strValue.Length == 3)
						{
							strValue = strValue.Replace("\"", "");
						}
						if (paramType.Name == "Char" && strValue.Length > 1)
						{
							return null;
						}
						convertedParams[i] = Convert.ChangeType(strValue, paramType);
					}
					else if (memoryStack != null)
					{
						var value = memoryStack.Get(paramValues[i].ToString());
						if (value != null)
						{
							convertedParams[i] = Convert.ChangeType(value, paramType);
						}
						else
						{
							convertedParams[i] = Convert.ChangeType(paramValues[i], paramType);
						}
					} else
					{
						convertedParams[i] = Convert.ChangeType(paramValues[i], paramType);
					}
				}
				
			}
			return convertedParams;
		}
	}
}
