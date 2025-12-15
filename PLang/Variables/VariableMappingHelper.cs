namespace PLang.Variables;

using PLang.Errors;
using PLang.Errors.Methods;
using PLang.Variables.Errors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MethodNotFoundError = Errors.MethodNotFoundError;

public class VariableMappingHelper
{
	private readonly Dictionary<string, Type> _pipedClasses;

	public VariableMappingHelper()
	{
		_pipedClasses = LoadPipedClasses();
	}

	private Dictionary<string, Type> LoadPipedClasses()
	{
		var discovery = new PipedClassDiscovery();
		var classes = discovery.GetPipedClasses();

		var dict = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
		foreach (var type in classes)
		{
			dict[type.Name] = type;
			dict[type.FullName] = type;
		}

		// Add built-in types
		dict["string"] = typeof(string);
		dict["int"] = typeof(int);
		dict["long"] = typeof(long);
		dict["decimal"] = typeof(decimal);
		dict["double"] = typeof(double);
		dict["bool"] = typeof(bool);
		dict["object"] = typeof(object);

		return dict;
	}

	public (RuntimeVariableMapping mapping, IError error) ValidateMapping(VariableMapping llmMapping)
	{
		var validatedMapping = new RuntimeVariableMapping
		{
			OriginalText = llmMapping.OriginalText,
			Variables = new List<RuntimeVariable>()
		};

		foreach (var llmVariable in llmMapping.Variables)
		{
			int start = llmMapping.OriginalText.IndexOf(llmVariable.FullExpression);
			if (start == -1)
			{
				return (null, new VariableNotFoundError(
					$"Variable expression '{llmVariable.FullExpression}' not found in original text"));
			}

			int end = start + llmVariable.FullExpression.Length;

			var (runtimeVar, error) = ValidateVariable(llmVariable);
			if (error != null)
			{
				return (null, error);
			}

			runtimeVar.Start = start;
			runtimeVar.End = end;

			validatedMapping.Variables.Add(runtimeVar);
		}

		return (validatedMapping, null);
	}
	public (RuntimeVariable variable, IError error) ValidateVariable(LlmVariable llmVariable)
	{
		var runtimeVariable = new RuntimeVariable
		{
			FullExpression = llmVariable.FullExpression,
			VariableName = llmVariable.VariableName,
			PropertyPaths = llmVariable.PropertyPaths,
			Operations = new List<RuntimeOperation>()
		};

		Type currentType = typeof(object);

		for (int i = 0; i < llmVariable.Operations.Count; i++)
		{
			var llmOp = llmVariable.Operations[i];
			var result = ValidateOperation(llmOp, currentType, i);

			if (result.error != null)
			{
				return (null, result.error);
			}

			runtimeVariable.Operations.Add(result.operation);
			currentType = result.returnType;
		}

		return (runtimeVariable, null);
	}

	private (RuntimeOperation operation, Type returnType, IError error) ValidateOperation(
		Operation llmOp, Type inputType, int operationIndex)
	{
		if (!_pipedClasses.TryGetValue(llmOp.Class, out Type classType))
		{
			return (null, null, new ClassNotFoundError(
				$"Class '{llmOp.Class}' not found in operation {operationIndex}"));
		}

		if (IsBuiltInOperation(llmOp.Method))
		{
			return ValidateBuiltInOperation(llmOp, inputType, operationIndex);
		}

		var method = FindMethod(classType, llmOp.Method, llmOp.Parameters);
		if (method == null)
		{
			return (null, null, new MethodNotFoundError(
				$"Method '{llmOp.Method}' not found on class '{llmOp.Class}' in operation {operationIndex}"));
		}

		var parameters = method.GetParameters();
		int paramOffset = method.IsStatic ? 0 : 1;
		int expectedParamCount = parameters.Length - paramOffset;

		if (llmOp.Parameters.Length != expectedParamCount)
		{
			return (null, null, new ParameterCountMismatchError(
				$"Method '{llmOp.Method}' expects {expectedParamCount} parameters but got {llmOp.Parameters.Length} in operation {operationIndex}"));
		}

		for (int i = 0; i < llmOp.Parameters.Length; i++)
		{
			var param = parameters[i + paramOffset];
			var providedValue = llmOp.Parameters[i];

			if (!CanConvertParameter(providedValue, param.ParameterType))
			{
				return (null, null, new ParameterTypeMismatchError(
					$"Parameter {i} of method '{llmOp.Method}' expects type '{param.ParameterType.Name}' but got '{providedValue?.GetType().Name ?? "null"}' in operation {operationIndex}"));
			}
		}

		Type returnType = GetReturnType(llmOp.ReturnType);
		if (returnType == null)
		{
			return (null, null, new InvalidReturnTypeError(
				$"Return type '{llmOp.ReturnType}' is not valid in operation {operationIndex}"));
		}

		var runtimeOp = new RuntimeOperation
		{
			Class = llmOp.Class,
			Method = llmOp.Method,
			Parameters = llmOp.Parameters,
			ReturnType = llmOp.ReturnType,
			MethodInfo = method
		};

		return (runtimeOp, returnType, null);
	}

	private bool IsBuiltInOperation(string method)
	{
		return method == "Column" || method == "Index" ||
			   method == "ToUpper" || method == "ToLower" ||
			   method == "Multiply" || method == "Add" ||
			   method == "Subtract" || method == "Divide" ||
			   method == "Increment" || method == "Decrement";
	}

	private (RuntimeOperation operation, Type returnType, IError error) ValidateBuiltInOperation(
		Operation llmOp, Type inputType, int operationIndex)
	{
		Type returnType = GetReturnType(llmOp.ReturnType);
		if (returnType == null)
		{
			return (null, null, new InvalidReturnTypeError(
				$"Return type '{llmOp.ReturnType}' is not valid in operation {operationIndex}"));
		}

		switch (llmOp.Method)
		{
			case "Column":
				if (llmOp.Parameters.Length != 1 || !(llmOp.Parameters[0] is string))
				{
					return (null, null, new ParameterValidationError(
						$"Column operation requires exactly one string parameter in operation {operationIndex}"));
				}
				break;

			case "Index":
				if (llmOp.Parameters.Length != 1)
				{
					return (null, null, new ParameterValidationError(
						$"Index operation requires exactly one parameter in operation {operationIndex}"));
				}
				break;

			case "Multiply":
			case "Add":
			case "Subtract":
			case "Divide":
				if (llmOp.Parameters.Length != 1)
				{
					return (null, null, new ParameterValidationError(
						$"{llmOp.Method} operation requires exactly one parameter in operation {operationIndex}"));
				}
				break;

			case "Increment":
			case "Decrement":
				if (llmOp.Parameters.Length != 0)
				{
					return (null, null, new ParameterValidationError(
						$"{llmOp.Method} operation requires no parameters in operation {operationIndex}"));
				}
				break;
		}

		var runtimeOp = new RuntimeOperation
		{
			Class = llmOp.Class,
			Method = llmOp.Method,
			Parameters = llmOp.Parameters,
			ReturnType = llmOp.ReturnType,
			MethodInfo = null
		};

		return (runtimeOp, returnType, null);
	}

	private MethodInfo FindMethod(Type classType, string methodName, object[] parameters)
	{
		var methods = classType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
			.Where(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase))
			.ToList();

		if (!methods.Any())
			return null;

		foreach (var method in methods)
		{
			var methodParams = method.GetParameters();
			int paramOffset = method.IsStatic ? 0 : 1;

			if (methodParams.Length - paramOffset == parameters.Length)
			{
				return method;
			}
		}

		return methods.FirstOrDefault();
	}

	private bool CanConvertParameter(object value, Type targetType)
	{
		if (value == null)
			return !targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null;

		var valueType = value.GetType();

		if (targetType.IsAssignableFrom(valueType))
			return true;

		if (IsNumericType(targetType) && IsNumericType(valueType))
			return true;

		if (valueType == typeof(string))
			return true;

		return false;
	}

	private bool IsNumericType(Type type)
	{
		return type == typeof(int) || type == typeof(long) ||
			   type == typeof(decimal) || type == typeof(double) ||
			   type == typeof(float) || type == typeof(short) ||
			   type == typeof(byte);
	}

	private Type GetReturnType(string typeName)
	{
		if (string.IsNullOrEmpty(typeName))
			return null;

		if (typeName.EndsWith("[]"))
		{
			var elementTypeName = typeName.Substring(0, typeName.Length - 2);
			var elementType = GetReturnType(elementTypeName);
			return elementType?.MakeArrayType();
		}

		if (_pipedClasses.TryGetValue(typeName, out Type type))
			return type;

		return Type.GetType(typeName);
	}
}