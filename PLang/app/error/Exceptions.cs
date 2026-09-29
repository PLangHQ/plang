namespace app.error;

/// <summary>
/// A program's error that has to travel as an exception — from a place that can't answer a result (a
/// constructor, a stream callback, a member with no Data return). It carries its <see cref="Error"/> whole;
/// every catch answers that Error.
/// </summary>
public class AppException : Exception
{
    public Error Error { get; }

    public AppException(Error error, Exception? inner = null)
        : base(error.Message, inner) => Error = error;

    public AppException(string message, string key = "AppError", int statusCode = 500)
        : this(new Error(message, key, statusCode)) { }

    public AppException(string message, Exception innerException, string key = "AppError", int statusCode = 500)
        : this(new Error(message, key, statusCode), innerException) { }
}

/// <summary>
/// A .pr in a shape this builder doesn't write — another (older) builder made it. The goal has to be rebuilt.
/// </summary>
public class PrFormatOutdatedException : AppException
{
    /// <summary>Why <paramref name="origin"/> — the .pr being read, when the read knows it — isn't this format.</summary>
    public PrFormatOutdatedException(string reason, global::app.type.item.path.@this? origin)
        : base($"{(origin != null ? $"{origin}: " : "")}{reason} — it was built by an older builder. Rebuild the goal.",
            "PrFormatOutdated", 400) { }
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
/// Exception thrown when a variable is not found in memory.
/// </summary>
public class VariableNotFoundException : AppException
{
    public string VariableName { get; }

    public VariableNotFoundException(string variableName)
        : base($"Variable %{variableName}% is not set, or is not reachable in the current context. "
             + "Check that it was assigned before this point, or that the dot-path navigation matches the value's actual shape.",
               "VariableNotFound", 404)
    {
        VariableName = variableName;
    }

    // Dotted-path failure: name WHERE the walk broke — the deepest prefix that resolved, its
    // runtime type, and the next segment that returned nothing. Turns an opaque "not reachable"
    // into "goal resolved to X, but .step was not found on it".
    public VariableNotFoundException(string variableName, string reachedPrefix, string reachedType, string failedSegment)
        : base($"Variable %{variableName}% is not reachable: navigated as far as '{reachedPrefix}' "
             + $"(a {reachedType}), but '.{failedSegment}' resolved to nothing on it. "
             + "Check the dot-path matches the value's actual shape.",
               "VariableNotFound", 404)
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

/// <summary>
/// A type declined to make a value from what it was handed (text that isn't a number, a dict that isn't a
/// whole goal channel), with the reason the type gave — its <see cref="AppException.Error"/>.
/// </summary>
public sealed class DeclinedException : AppException
{
    public DeclinedException(Error error) : base(error) { }
}
