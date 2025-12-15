namespace PLang.Variables;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class PipeExecutor
{
	private readonly Dictionary<string, object> _variables;
	private Dictionary<string, Func<object, object[], object>> _methods;

	public PipeExecutor(Dictionary<string, object> variables)
	{
		_variables = variables;
		InitializeMethods();
	}

	private void InitializeMethods()
	{
		_methods = new Dictionary<string, Func<object, object[], object>>
		{
			["ToUpper"] = (input, parameters) => input?.ToString()?.ToUpper(),
			["ToLower"] = (input, parameters) => input?.ToString()?.ToLower(),
			["Split"] = (input, parameters) => input?.ToString()?.Split((string)parameters[0]),
			["Trim"] = (input, parameters) => input?.ToString()?.Trim(),
			["TrimStart"] = (input, parameters) => input?.ToString()?.TrimStart(),
			["TrimEnd"] = (input, parameters) => input?.ToString()?.TrimEnd(),
			["Substring"] = (input, parameters) => parameters.Length == 1
				? input?.ToString()?.Substring(Convert.ToInt32(parameters[0]))
				: input?.ToString()?.Substring(Convert.ToInt32(parameters[0]), Convert.ToInt32(parameters[1])),
			["Replace"] = (input, parameters) => input?.ToString()?.Replace((string)parameters[0], (string)parameters[1]),
			["Contains"] = (input, parameters) => input?.ToString()?.Contains((string)parameters[0]) ?? false,
			["StartsWith"] = (input, parameters) => input?.ToString()?.StartsWith((string)parameters[0]) ?? false,
			["EndsWith"] = (input, parameters) => input?.ToString()?.EndsWith((string)parameters[0]) ?? false,

			// Arithmetic operations
			["Increment"] = (input, parameters) => IncrementNumber(input),
			["Decrement"] = (input, parameters) => DecrementNumber(input),
			["Add"] = (input, parameters) => AddNumbers(input, parameters[0]),
			["Subtract"] = (input, parameters) => SubtractNumbers(input, parameters[0]),
			["Multiply"] = (input, parameters) => MultiplyNumbers(input, parameters[0]),
			["Divide"] = (input, parameters) => DivideNumbers(input, parameters[0]),
			["Modulo"] = (input, parameters) => ModuloNumbers(input, parameters[0]),

			// Aggregate functions
			["Average"] = (input, parameters) => CalculateAverage(input, parameters),
			["Sum"] = (input, parameters) => CalculateSum(input, parameters),
			["Min"] = (input, parameters) => CalculateMin(input, parameters),
			["Max"] = (input, parameters) => CalculateMax(input, parameters),
			["Count"] = (input, parameters) => CalculateCount(input, parameters),
		};
	}

	public object Execute(string variableName, PipelineResult pipeline)
	{
		if (!_variables.ContainsKey(variableName))
			throw new KeyNotFoundException($"Variable '{variableName}' not found");

		object current = _variables[variableName];

		foreach (var operation in pipeline.Operations)
		{
			current = ExecuteOperation(current, operation);
		}

		return current;
	}

	private object ExecuteOperation(object input, RuntimeOperation operation)
	{
		switch (operation.Method)
		{
			case "Column":
				return GetColumn(input, (string)operation.Parameters[0]);

			case "Index":
				return GetIndex(input, Convert.ToInt32(operation.Parameters[0]));

			default:
				// Check if it's a built-in method
				if (_methods.ContainsKey(operation.Method))
				{
					return _methods[operation.Method](input, operation.Parameters);
				}

				// Otherwise use reflection with cached MethodInfo
				return InvokeMethod(input, operation);
		}
	}

	private object GetColumn(object obj, string columnName)
	{
		if (obj == null)
			return null;

		// 1. Check if it's a Dictionary
		if (obj is IDictionary<string, object> dict)
		{
			return dict.ContainsKey(columnName) ? dict[columnName] : null;
		}

		// 2. Check if it's a JSON object (JObject from Newtonsoft)
		if (obj is Newtonsoft.Json.Linq.JObject jObject)
		{
			return jObject[columnName]?.ToObject<object>();
		}

		// 3. Check if it's a JSON object (JsonElement from System.Text.Json)
		if (obj is System.Text.Json.JsonElement jsonElement && jsonElement.ValueKind == System.Text.Json.JsonValueKind.Object)
		{
			if (jsonElement.TryGetProperty(columnName, out var property))
			{
				return JsonElementToObject(property);
			}
			return null;
		}

		// 4. Check if it's a DataRow
		if (obj is System.Data.DataRow dataRow)
		{
			return dataRow.Table.Columns.Contains(columnName) ? dataRow[columnName] : null;
		}

		// 5. Fall back to reflection for regular classes
		var type = obj.GetType();
		var propertyInfo = type.GetProperty(columnName,
			System.Reflection.BindingFlags.Public |
			System.Reflection.BindingFlags.Instance |
			System.Reflection.BindingFlags.IgnoreCase);

		if (propertyInfo != null)
		{
			return propertyInfo.GetValue(obj);
		}

		var fieldInfo = type.GetField(columnName,
			System.Reflection.BindingFlags.Public |
			System.Reflection.BindingFlags.Instance |
			System.Reflection.BindingFlags.IgnoreCase);

		if (fieldInfo != null)
		{
			return fieldInfo.GetValue(obj);
		}

		throw new InvalidOperationException($"Cannot find column '{columnName}' on type {type.Name}");
	}

	private object GetIndex(object obj, int index)
	{
		if (obj == null)
			return null;

		// 1. Check if it's an array
		if (obj is Array array)
		{
			return array.GetValue(index);
		}

		// 2. Check if it's a List<T> or IList
		if (obj is IList list)
		{
			return list[index];
		}

		// 3. Check if it's a JSON array (JArray from Newtonsoft)
		if (obj is Newtonsoft.Json.Linq.JArray jArray)
		{
			return jArray[index]?.ToObject<object>();
		}

		// 4. Check if it's a JSON array (JsonElement from System.Text.Json)
		if (obj is System.Text.Json.JsonElement jsonElement && jsonElement.ValueKind == System.Text.Json.JsonValueKind.Array)
		{
			return JsonElementToObject(jsonElement[index]);
		}

		// 5. Check if it's an IEnumerable (fallback)
		if (obj is IEnumerable enumerable)
		{
			return enumerable.Cast<object>().ElementAtOrDefault(index);
		}

		throw new InvalidOperationException($"Cannot index into type {obj.GetType().Name}");
	}

	private object InvokeMethod(object input, RuntimeOperation operation)
	{
		if (operation.MethodInfo == null)
		{
			throw new InvalidOperationException($"MethodInfo not set for operation '{operation.Method}'");
		}

		if (operation.MethodInfo.IsStatic)
		{
			return operation.MethodInfo.Invoke(null, new[] { input }.Concat(operation.Parameters).ToArray());
		}
		else
		{
			return operation.MethodInfo.Invoke(input, operation.Parameters);
		}
	}

	private object JsonElementToObject(System.Text.Json.JsonElement element)
	{
		switch (element.ValueKind)
		{
			case System.Text.Json.JsonValueKind.String:
				return element.GetString();
			case System.Text.Json.JsonValueKind.Number:
				if (element.TryGetInt32(out int intValue))
					return intValue;
				if (element.TryGetInt64(out long longValue))
					return longValue;
				return element.GetDouble();
			case System.Text.Json.JsonValueKind.True:
				return true;
			case System.Text.Json.JsonValueKind.False:
				return false;
			case System.Text.Json.JsonValueKind.Null:
				return null;
			case System.Text.Json.JsonValueKind.Object:
			case System.Text.Json.JsonValueKind.Array:
				return element;
			default:
				return element.ToString();
		}
	}

	#region Arithmetic Operations

	private object IncrementNumber(object value)
	{
		return value switch
		{
			int intValue => intValue + 1,
			long longValue => longValue + 1,
			decimal decimalValue => decimalValue + 1,
			double doubleValue => doubleValue + 1,
			float floatValue => floatValue + 1,
			_ => Convert.ToInt32(value) + 1
		};
	}

	private object DecrementNumber(object value)
	{
		return value switch
		{
			int intValue => intValue - 1,
			long longValue => longValue - 1,
			decimal decimalValue => decimalValue - 1,
			double doubleValue => doubleValue - 1,
			float floatValue => floatValue - 1,
			_ => Convert.ToInt32(value) - 1
		};
	}

	private object AddNumbers(object a, object b)
	{
		var numB = ParseNumericParameter(b);

		return a switch
		{
			int intA => intA + (int)numB,
			long longA => longA + (long)numB,
			decimal decimalA => decimalA + (decimal)numB,
			double doubleA => doubleA + numB,
			float floatA => floatA + (float)numB,
			_ => Convert.ToDouble(a) + numB
		};
	}

	private object SubtractNumbers(object a, object b)
	{
		var numB = ParseNumericParameter(b);

		return a switch
		{
			int intA => intA - (int)numB,
			long longA => longA - (long)numB,
			decimal decimalA => decimalA - (decimal)numB,
			double doubleA => doubleA - numB,
			float floatA => floatA - (float)numB,
			_ => Convert.ToDouble(a) - numB
		};
	}

	private object MultiplyNumbers(object a, object b)
	{
		var numB = ParseNumericParameter(b);

		return a switch
		{
			int intA => intA * numB,
			long longA => longA * numB,
			decimal decimalA => decimalA * (decimal)numB,
			double doubleA => doubleA * numB,
			float floatA => floatA * (float)numB,
			_ => Convert.ToDouble(a) * numB
		};
	}

	private object DivideNumbers(object a, object b)
	{
		var numB = ParseNumericParameter(b);

		if (Math.Abs(numB) < double.Epsilon)
			throw new DivideByZeroException("Cannot divide by zero");

		return a switch
		{
			int intA => intA / numB,
			long longA => longA / numB,
			decimal decimalA => decimalA / (decimal)numB,
			double doubleA => doubleA / numB,
			float floatA => floatA / (float)numB,
			_ => Convert.ToDouble(a) / numB
		};
	}

	private object ModuloNumbers(object a, object b)
	{
		var numB = ParseNumericParameter(b);

		return a switch
		{
			int intA => intA % (int)numB,
			long longA => longA % (long)numB,
			decimal decimalA => decimalA % (decimal)numB,
			double doubleA => doubleA % numB,
			float floatA => floatA % (float)numB,
			_ => Convert.ToDouble(a) % numB
		};
	}

	private double ParseNumericParameter(object param)
	{
		if (param is string str && str.EndsWith("%"))
		{
			var percentValue = double.Parse(str.TrimEnd('%'));
			return percentValue / 100.0;
		}

		return Convert.ToDouble(param);
	}

	#endregion

	#region Aggregate Functions

	private object CalculateAverage(object input, object[] parameters)
	{
		if (input is not IEnumerable enumerable)
			throw new InvalidOperationException("Average requires an enumerable input");

		var items = enumerable.Cast<object>().ToList();

		if (parameters.Length > 0 && parameters[0] is string columnName)
		{
			var values = items.Select(item => GetColumn(item, columnName))
							 .Where(v => v != null)
							 .Select(v => Convert.ToDouble(v))
							 .ToList();
			return values.Any() ? values.Average() : 0;
		}

		return items.Select(v => Convert.ToDouble(v)).Average();
	}

	private object CalculateSum(object input, object[] parameters)
	{
		if (input is not IEnumerable enumerable)
			throw new InvalidOperationException("Sum requires an enumerable input");

		var items = enumerable.Cast<object>().ToList();

		if (parameters.Length > 0 && parameters[0] is string columnName)
		{
			return items.Select(item => GetColumn(item, columnName))
					   .Where(v => v != null)
					   .Select(v => Convert.ToDouble(v))
					   .Sum();
		}

		return items.Select(v => Convert.ToDouble(v)).Sum();
	}

	private object CalculateMin(object input, object[] parameters)
	{
		if (input is not IEnumerable enumerable)
			throw new InvalidOperationException("Min requires an enumerable input");

		var items = enumerable.Cast<object>().ToList();

		if (parameters.Length > 0 && parameters[0] is string columnName)
		{
			return items.Select(item => GetColumn(item, columnName))
					   .Where(v => v != null)
					   .Select(v => Convert.ToDouble(v))
					   .Min();
		}

		return items.Select(v => Convert.ToDouble(v)).Min();
	}

	private object CalculateMax(object input, object[] parameters)
	{
		if (input is not IEnumerable enumerable)
			throw new InvalidOperationException("Max requires an enumerable input");

		var items = enumerable.Cast<object>().ToList();

		if (parameters.Length > 0 && parameters[0] is string columnName)
		{
			return items.Select(item => GetColumn(item, columnName))
					   .Where(v => v != null)
					   .Select(v => Convert.ToDouble(v))
					   .Max();
		}

		return items.Select(v => Convert.ToDouble(v)).Max();
	}

	private object CalculateCount(object input, object[] parameters)
	{
		if (input is not IEnumerable enumerable)
			throw new InvalidOperationException("Count requires an enumerable input");

		return enumerable.Cast<object>().Count();
	}

	#endregion
}