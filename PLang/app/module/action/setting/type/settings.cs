using app;

namespace app.module.action.setting;

public static class type
{
    public sealed class setting : global::app.type.item.@this, global::app.type.item.ICreate<setting>
    {
        [Out, Store] public string? key { get; init; }
        [Out, Masked, Store] public object? value { get; init; }
    }
}
