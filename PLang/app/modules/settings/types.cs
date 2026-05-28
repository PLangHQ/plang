using app;

namespace app.modules.settings;

public static class types
{
    public record setting
    {
        [Out, Store] public string? key { get; init; }
        [Out, Masked, Store] public object? value { get; init; }
    }
}
