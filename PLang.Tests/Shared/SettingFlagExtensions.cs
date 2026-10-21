namespace PLang.Tests.Shared;

/// <summary>A value written as the command line writes a flag (<c>--&lt;path&gt;=&lt;value&gt;</c>): the store's door for
/// flags checks it and answers where each value it sets is kept — then the store's blind Set of each. A refusal is the
/// answer, and nothing is written.</summary>
public static class SettingFlagExtensions
{
    public static async Task<global::app.data.@this> Flag(this global::app.actor.setting.@this store, string path, object? value,
        global::app.actor.context.@this context)
    {
        var values = await store.Flags([new(path, value)], context);
        if (!values.Success) return values;
        foreach (var entry in (await values.Value())!.Entries(context)) store.Set(entry.Name, entry);
        return values;
    }
}
