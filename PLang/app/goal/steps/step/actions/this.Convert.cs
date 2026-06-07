namespace app.goal.steps.step.actions;

public sealed partial class @this
{
    /// <summary>
    /// OBP: a structural action chain owns its reconstruction. <c>error.handle.Actions</c>
    /// (an on-error recovery chain) rides the born-native wire as a list of action records;
    /// each row rebuilds through <see cref="action.@this.FromWire"/>. Declines (<c>null</c>)
    /// anything that isn't a sequence so the dispatcher falls through.
    /// </summary>
    public static global::app.data.@this? Convert(object? value, string? kind,
        global::app.actor.context.@this context)
    {
        if (value is @this already) return global::app.data.@this.Ok(already);
        if (action.@this.Sequence(value) is not { } seq) return null;

        var actions = new @this();
        foreach (var element in seq)
        {
            var raw = element is global::app.data.@this d ? d.Value : element;
            if (action.@this.FromWire(raw, context) is { } act) actions.Add(act);
        }
        return global::app.data.@this.Ok(actions);
    }
}
