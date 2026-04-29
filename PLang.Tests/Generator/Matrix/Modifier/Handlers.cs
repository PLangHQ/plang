namespace App.modules.matrix.modifier;

[global::App.modules.Action("modifieraction", Cacheable = false)]
[global::App.modules.Modifier(Order = 1)]
public partial class ModifierAction : global::App.modules.IContext, global::App.modules.IModifier
{
    public partial global::App.Data.@this<string> Tag { get; init; }

    public Task<global::App.Data.@this> Run() => Task.FromResult(global::App.Data.@this.Ok());

    public Func<Task<global::App.Data.@this>> Wrap(
        Func<Task<global::App.Data.@this>> next,
        global::App.Actor.Context.@this context)
    {
        return async () =>
        {
            var result = await next();
            // Tag-pass-through: append the modifier's Tag to the result so tests can verify wrap fired.
            if (result.Success && result.Value is string s)
                return global::App.Data.@this.Ok($"{s}|{Tag.Value}");
            return result;
        };
    }
}
