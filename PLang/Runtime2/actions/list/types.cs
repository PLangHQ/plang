namespace PLang.Runtime2.actions.list.types;

public record list
{
    public int count { get; init; }
    public object? value { get; init; }
}
