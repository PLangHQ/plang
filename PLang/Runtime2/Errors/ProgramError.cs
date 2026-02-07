using PLang.Runtime2.Context;

namespace PLang.Runtime2.Errors;

/// <summary>
/// Legacy error type kept for backward compatibility.
/// </summary>
public class ProgramError : Error
{
    public ProgramError(string message, string key = "ProgramError", int statusCode = 400)
        : base(message, key, statusCode) { }

    public ProgramError(string message, PLangContext context, string key = "ProgramError", int statusCode = 400)
        : base(message, context, key, statusCode) { }

    public new static ProgramError FromException(Exception ex, string key = "Exception", int statusCode = 500)
    {
        return new ProgramError(ex.Message, key, statusCode)
        {
            Exception = ex,
            InnerError = ex.InnerException != null ? FromException(ex.InnerException) : null
        };
    }

    public new static ProgramError FromException(Exception ex, PLangContext context, string key = "Exception", int statusCode = 500)
    {
        return new ProgramError(ex.Message, context, key, statusCode)
        {
            Exception = ex,
            InnerError = ex.InnerException != null ? FromException(ex.InnerException) : null
        };
    }
}
