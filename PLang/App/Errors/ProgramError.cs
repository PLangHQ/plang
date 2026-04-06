using App.Context;

namespace App.Errors;

/// <summary>
/// Legacy error type kept for backward compatibility.
/// </summary>
public class ProgramError : Error
{
    public ProgramError(string message, string key = "ProgramError", int statusCode = 400)
        : base(message, key, statusCode) { }

    public ProgramError(string message, Step step, string key = "ProgramError", int statusCode = 400)
        : base(message, step, key, statusCode) { }

    public ProgramError(string message, Context.@this context, string key = "ProgramError", int statusCode = 400)
        : base(message, context, key, statusCode) { }

    public new static ProgramError FromException(Exception ex, string key = "Exception", int statusCode = 500)
    {
        return new ProgramError(ex.Message, key, statusCode)
        {
            Exception = ex
        };
    }

    public new static ProgramError FromException(Exception ex, Context.@this context, string key = "Exception", int statusCode = 500)
    {
        return new ProgramError(ex.Message, context, key, statusCode)
        {
            Exception = ex
        };
    }
}
