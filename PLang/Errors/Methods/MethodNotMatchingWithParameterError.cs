namespace PLang.Errors.Methods;

public record MethodNotMatchingWithParametersError(string Message, string MethodName, Type Type, IError ParameterErrors) : Error(Message, "MethodNotMatchingWithParameters", 500)
{
    public Type Type { get; init; } = Type;
    public string MethodName { get; init; } = MethodName;
    public IError ParameterErrors { get; init; } = ParameterErrors;
}