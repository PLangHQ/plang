using app;

namespace app.modules.list.types;

public record list
{
    [Out] public int count { get; init; }
    [Out] public object? value { get; init; }
}
