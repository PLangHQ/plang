namespace App.Engine.Errors;

/// <summary>
/// Base exception for App errors.
/// </summary>
public class AppException : Exception
{
    public string Key { get; }
    public int StatusCode { get; }

    public AppException(string message, string key = "AppError", int statusCode = 500)
        : base(message)
    {
        Key = key;
        StatusCode = statusCode;
    }

    public AppException(string message, Exception innerException, string key = "AppError", int statusCode = 500)
        : base(message, innerException)
    {
        Key = key;
        StatusCode = statusCode;
    }
}

/// <summary>
/// Exception thrown when a goal is not found.
/// </summary>
public class GoalNotFoundException : AppException
{
    public string GoalName { get; }

    public GoalNotFoundException(string goalName)
        : base($"Goal '{goalName}' not found", "GoalNotFound", 404)
    {
        GoalName = goalName;
    }
}

/// <summary>
/// Exception thrown when a step fails to execute.
/// </summary>
public class StepExecutionException : AppException
{
    public int StepIndex { get; }

    public StepExecutionException(string message, int stepIndex)
        : base(message, "StepExecutionFailed", 500)
    {
        StepIndex = stepIndex;
    }

    public StepExecutionException(string message, int stepIndex, Exception innerException)
        : base(message, innerException, "StepExecutionFailed", 500)
    {
        StepIndex = stepIndex;
    }
}

/// <summary>
/// Exception thrown when a module is not found.
/// </summary>
public class ModuleNotFoundException : AppException
{
    public string ModuleName { get; }

    public ModuleNotFoundException(string moduleName)
        : base($"Module '{moduleName}' not found", "ModuleNotFound", 404)
    {
        ModuleName = moduleName;
    }
}

/// <summary>
/// Exception thrown when an action is not found.
/// </summary>
public class ActionNotFoundException : AppException
{
    public string ActionName { get; }

    public ActionNotFoundException(string actionName)
        : base($"Action '{actionName}' not found", "ActionNotFound", 404)
    {
        ActionName = actionName;
    }
}

/// <summary>
/// Exception thrown when a variable is not found in memory.
/// </summary>
public class VariableNotFoundException : AppException
{
    public string VariableName { get; }

    public VariableNotFoundException(string variableName)
        : base($"Variable '{variableName}' not found", "VariableNotFound", 404)
    {
        VariableName = variableName;
    }
}

/// <summary>
/// Exception thrown when call stack depth exceeds the limit.
/// </summary>
public class CallStackOverflowException : AppException
{
    public int MaxDepth { get; }

    public CallStackOverflowException(int maxDepth)
        : base($"Call stack overflow: exceeded {maxDepth} frames", "CallStackOverflow", 500)
    {
        MaxDepth = maxDepth;
    }
}

/// <summary>
/// Exception thrown when serialization fails.
/// </summary>
public class SerializationException : AppException
{
    public Type? TargetType { get; }

    public SerializationException(string message, Type? targetType = null)
        : base(message, "SerializationFailed", 500)
    {
        TargetType = targetType;
    }

    public SerializationException(string message, Exception innerException, Type? targetType = null)
        : base(message, innerException, "SerializationFailed", 500)
    {
        TargetType = targetType;
    }
}
