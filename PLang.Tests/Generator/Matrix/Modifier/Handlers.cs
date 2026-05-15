namespace app.modules.matrix.modifier;

[global::app.modules.Action("modifieraction", Cacheable = false)]
[global::app.modules.Modifier(Order = 1)]
public partial class ModifierAction : global::app.modules.IContext, global::app.modules.IModifier
{
    public partial global::app.Data.@this<string> Tag { get; init; }

    public Task<global::app.Data.@this> Run() => Task.FromResult(global::app.Data.@this.Ok());

    public Func<Task<global::app.Data.@this>> Wrap(
        Func<Task<global::app.Data.@this>> next,
        global::app.Actor.Context.@this context)
    {
        return async () =>
        {
            var result = await next();
            // Tag-pass-through: append the modifier's Tag to the result so tests can verify wrap fired.
            if (result.Success && result.Value is string s)
                return global::app.Data.@this.Ok($"{s}|{Tag.Value}");
            return result;
        };
    }
}
