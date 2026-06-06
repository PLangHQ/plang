using app;

namespace app.module.settings;

public static class type
{
    public sealed class setting : global::app.type.item.@this
    {
        [Out, Store] public string? key { get; init; }
        [Out, Masked, Store] public object? value { get; init; }
    }
}
