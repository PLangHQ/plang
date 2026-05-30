using app;

namespace app.module.settings;

public static class type
{
    public record setting
    {
        [Out, Store] public string? key { get; init; }
        [Out, Masked, Store] public object? value { get; init; }
    }
}
