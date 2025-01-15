namespace PLang.Errors.Methods;

public record MethodNotFoundError(string Message, string MethodName, Type Type) : Error(Message, "MethodNotFound", 500)
{
    public string MethodName => MethodName;
    public Type Type { get; init; } = Type;
}