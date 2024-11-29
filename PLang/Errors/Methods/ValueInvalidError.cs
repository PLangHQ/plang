namespace PLang.Errors.Methods;

public record ValueInvalidError(string Message, object? Value, Type? Type) : Error(Message, "ValueInvalid", 500)
{
   
    public object? Value { get; set; } = Value;
    public Type? Type { get; set; } = Type;
    
}