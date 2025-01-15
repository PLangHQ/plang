using System.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Errors;
using PLang.Errors.Methods;

namespace PLang.Building.Model;

public class MethodExecution
{
	public required string MethodName { get; set; }
	[Description("Give the input a name using snake_case")]
	public required string Name { get; set; }
	[Description("Write description of what the step does")]
	public required string Description { get; set; }
	[Description("Indicates if step should run and forget (in new thread)")]
	public required bool WaitForExecution { get; set; } = false;
	public List<ParameterDescriptionResponse> Parameters { get; set; }
	public List<ReturnValueResponse>? ReturnType { get; set; }

	public MethodExecution()
	{
		Parameters = new List<ParameterDescriptionResponse>();
	}

	public (T?, IError?) GetParameter<T>(string parameterName)
	{
		var targetType = typeof(T);
		var result = GetParameter(parameterName, targetType);
		return ((T?)result.Item1, result.Item2);
	}
	public (object? Instance, IError? Error) GetParameter(string parameterName, Type targetType)
	{
		if (Parameters == null) return (null, new ParameterNotFoundError($"No parameters for method. Parameter {parameterName} was not found.", targetType));
		foreach (ParameterDescriptionResponse parameter in Parameters)
		{
			if (parameter.Name == parameterName)
			{
				return (parameter.GetValue(targetType), null);
			}
		}
		return (null, new ParameterNotFoundError($"Parameter {parameterName} was not found.", targetType));
	}

}
public class ParameterDescriptionResponse
{
	public string Type { get; init; }
	public string Name { get; init; }
	[Description("Value can be a primitive object such as int, string, etc. or List<Parameters> when type is complex")]
	public object? Value { get; init; }

	public Type? GetType()
	{
		return System.Type.GetType(this.Type);
	}

	public T? GetValue<T>()
	{
		var targetType = typeof(T);
		return (T?)GetValue(targetType);
	}

	public object? GetValue(Type targetType)
	{
		if (Value == null) return null;

		// Handle cases where the type is a primitive or matches exactly
		if (Value.GetType() == targetType)
		{
			return Value;
		}

		if (Value is JToken jToken)
		{
			return jToken.ToObject(targetType);
		}

		// Handle deserialization for complex objects
		var valueJson = JsonConvert.SerializeObject(Value);

		try
		{
			return JsonConvert.DeserializeObject(valueJson, targetType);
		}
		catch (JsonException ex)
		{
			throw new InvalidOperationException(
				$"Failed to deserialize Value for parameter '{Name}' to type '{targetType.FullName}'. Expected type based on 'Type' property: {Type}.",
				ex
			);
		}

	}
}
public class ReturnValueResponse
{
	public string Type { get; init; }
	[Description("The %variable% that is being written into")]
	public string VariableName { get; init; }
	public int? DeleteAfterNumberOfUsage { get; init; } = null;
	public int? DeleteAfterSeconds { get; init; } = null;
}