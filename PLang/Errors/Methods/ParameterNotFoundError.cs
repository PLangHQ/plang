namespace PLang.Errors.Methods;

public record ParameterNotFoundError(string Message, Type? TargetType) : Error(Message, "ParameterNotFound", 500)
{
    public Type? Type { get; set; } = TargetType;
}