namespace App.Errors;

/// <summary>
/// Classifies the origin/intent of an error.
/// </summary>
public enum ErrorCategory
{
    /// <summary>
    /// Developer intentionally created this error (throw error, validate, business rule).
    /// The PLang developer chose to raise this — it's part of the application logic.
    /// </summary>
    Application,

    /// <summary>
    /// Unexpected system/runtime failure (file not found, null reference, timeout, handler crash).
    /// The PLang developer did not anticipate this error.
    /// </summary>
    Runtime
}
