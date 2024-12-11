namespace PLang.Errors.Methods;

public record ClassNotFound(string Message, string ClassName) : Error(Message, "ClassNotFound", 500)
{
    public string ClassName { get; init; } = ClassName;
}