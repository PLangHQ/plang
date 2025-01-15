namespace PLang.Errors.Methods;

public record MethodNotMatchingWithParametersError(string Message, string MethodName, Type Type, MultipleError ParameterErrors) : Error(Message, "MethodNotMatchingWithParameters", 500)
{
    public Type Type { get; init; } = Type;
    public string MethodName { get; init; } = MethodName;
    public MultipleError ParameterErrors { get; init; } = ParameterErrors;
}