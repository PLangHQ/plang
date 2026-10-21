namespace PLang.Tests.Shared;

/// <summary>A value written as the command line writes a flag (<c>--&lt;path&gt;=&lt;value&gt;</c>): the setting the path
/// finds takes it (or refuses it) and answers the address it is kept at — then the store's blind Set. A refusal on the
/// way is the answer, and nothing is written.</summary>
public static class SettingFlagExtensions
{
    public static async Task<global::app.data.@this> Flag(this global::app.actor.setting.@this store, string path, object? value,
        global::app.actor.context.@this context)
    {
        var setting = store.Find(path);
        if (!setting.Success) return setting;
        var address = (await setting.Value())!.Apply(path, value, context);
        if (!address.Success) return address;
        return store.Set((await address.Value())!.ToString(), value);
    }
}
