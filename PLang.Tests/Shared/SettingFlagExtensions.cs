namespace PLang.Tests.Shared;

/// <summary>A value written as the command line writes a flag (<c>--&lt;path&gt;=&lt;value&gt;</c>): the store's door for
/// flags checks it, writes it, and answers where each value it set is kept. A refusal is the answer, and nothing is
/// written.</summary>
public static class SettingFlagExtensions
{
    public static async Task<global::app.data.@this> Flag(this global::app.actor.setting.@this store, string path, object? value,
        global::app.actor.context.@this context)
        => await store.Flags([new(path, value)], context);
}
