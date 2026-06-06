namespace app.module.matrix.modifier;

[global::app.module.Action("modifieraction", Cacheable = false)]
[global::app.module.Modifier(Order = 1)]
public partial class ModifierAction : global::app.module.IContext, global::app.module.IModifier
{
    public partial global::app.data.@this<global::app.type.text.@this> Tag { get; init; }

    public Task<global::app.data.@this> Run() => Task.FromResult(global::app.data.@this.Ok());

    public Func<Task<global::app.data.@this>> Wrap(
        Func<Task<global::app.data.@this>> next,
        global::app.actor.context.@this context)
    {
        return async () =>
        {
            var result = await next();
            // Tag-pass-through: append the modifier's Tag to the result so tests can verify wrap fired.
            if (result.Success && result.Value is string s)
                return global::app.data.@this.Ok($"{s}|{Tag.Value}");
            return result;
        };
    }
}
