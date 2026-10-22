namespace PLang.Tests.Shared;

/// <summary>A value written as the command line writes a flag (<c>--&lt;path&gt;=&lt;value&gt;</c>): the start it makes
/// checks it, writes it into the store, and answers where each value it set is kept. A refusal is the answer, and
/// nothing is written.</summary>
public static class SettingFlagExtensions
{
    public static async Task<global::app.data.@this> Flag(this global::app.actor.setting.@this store, string path, object? value,
        global::app.actor.context.@this context)
        => await global::app.start.@this.Parse([$"--{path}={System.Text.Json.JsonSerializer.Serialize(value)}"]).Set(store, context);
}
