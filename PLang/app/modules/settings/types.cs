using app;

namespace app.modules.settings;

public static class types
{
    public record setting
    {
        [Out] public string? key { get; init; }
        [Out, Masked] public object? value { get; init; }
    }
}
