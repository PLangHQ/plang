using PLang.Building.Model;
using PLang.Errors;

namespace PLang.Utils;

public class StepHelper
{
    public static ErrorHandler? GetErrorHandlerForStep(List<ErrorHandler>? errorHandlers, IError error)
    {
        if (errorHandlers == null) return null;

        foreach (var errorHandler in errorHandlers)
        {
            if (string.IsNullOrEmpty(errorHandler.Message) &&
                string.IsNullOrEmpty(errorHandler.Key) &&
                errorHandler.StatusCode == null)
                return errorHandler;

            if (!string.IsNullOrEmpty(errorHandler.Message) &&
                error.Message.Contains(errorHandler.Message, StringComparison.OrdinalIgnoreCase)) return errorHandler;

            if (!string.IsNullOrEmpty(errorHandler.Key) &&
                error.Key.Equals(errorHandler.Key, StringComparison.OrdinalIgnoreCase)) return errorHandler;

            if (errorHandler.StatusCode != null && error.StatusCode == errorHandler.StatusCode) return errorHandler;
        }

        return null;
    }
}