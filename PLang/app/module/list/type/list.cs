using app;

namespace app.module.list.type;

public record list
{
    [Out] public int count { get; init; }
    [Out] public object? value { get; init; }
}
