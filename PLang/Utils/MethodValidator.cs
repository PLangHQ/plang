using Newtonsoft.Json.Linq;
using PLang.Errors;
using PLang.Errors.Methods;
using System.Collections;
using System.Reflection;

namespace PLang.Utils;

public class MethodValidator
{
	public GroupedErrors? Validate(string moduleName, string methodName, Dictionary<string, object> parameters)
	{
		var errors = new GroupedErrors();

		var type = Type.GetType(moduleName)
			?? AppDomain.CurrentDomain.GetAssemblies()
				.SelectMany(a => a.GetTypes())
				.FirstOrDefault(t => t.Name == moduleName || t.FullName == moduleName);

		if (type == null)
		{
			errors.Add(new InvalidParameterError("", $"Module '{moduleName}' not found", null));
			return errors;
		}

		var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
		if (method == null)
		{
			errors.Add(new InvalidParameterError("", $"Method '{methodName}' not found on '{moduleName}'", null));
			return errors;
		}

		var methodParams = method.GetParameters();
		var paramLookup = parameters.ToDictionary(k => k.Key, k => k.Value, StringComparer.OrdinalIgnoreCase);

		// Missing required
		foreach (var param in methodParams)
		{
			if (!paramLookup.ContainsKey(param.Name) && !param.HasDefaultValue)
				errors.Add(new InvalidParameterError(methodName, $"Missing required parameter: '{param.Name}'", null));
		}

		var validParams = methodParams.ToDictionary(p => p.Name!, p => p.ParameterType, StringComparer.OrdinalIgnoreCase);
		foreach (var parameter in parameters)
		{
			if (!validParams.TryGetValue(parameter.Key, out var parameterType))
			{
				errors.Add(new InvalidParameterError(methodName, $"Unknown parameter: '{parameter.Key}'", null));
				continue;
			}
			if (parameter.Value == null) continue;

			var obj = TypeHelper.ConvertToType(parameter.Value, parameterType);
			if (obj != null && obj.GetType() == parameterType)
			{
				int i = 0;
			} else
			{
				int b = 0;
			}
			// use obj...
		}

		/*
		// Unknown parameters
		var validNames = methodParams.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
		foreach (var key in parameters.Keys)
		{
			if (!validNames.Contains(key))
				errors.Add(new InvalidParameterError(methodName, $"Unknown parameter: '{key}'", null));
		}*/

		return (errors.Count == 0) ? null : errors;
	}
}