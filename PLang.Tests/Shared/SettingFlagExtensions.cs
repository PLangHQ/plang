namespace PLang.Tests.Shared;

/// <summary>A value written as the command line writes a flag (<c>--&lt;path&gt;=&lt;value&gt;</c>): the setting the path
/// finds, the option the setting says it names, the value its Apply takes — then the store's blind Set. A refusal on the
/// way is the answer, and nothing is written.</summary>
public static class SettingFlagExtensions
{
    public static async Task<global::app.data.@this> Flag(this global::app.actor.setting.@this store, string path, object? value,
        global::app.actor.context.@this context)
    {
        var found = store.Find(path);
        if (!found.Success) return found;
        var setting = (global::app.type.item.setting.@this)found.Peek()!;
        var option = setting.Option(path, context);
        if (!option.Success) return option;
        var named = (await option.Value())!.ToString();
        var fits = setting.Apply(named, value, context);
        if (!fits.Success) return fits;
        return store.Set(named.Length == 0 ? setting.Path : $"{setting.Path}.{named}", value);
    }
}
