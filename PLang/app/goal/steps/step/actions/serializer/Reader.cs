namespace app.goal.steps.step.actions.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) reader for <c>actions</c> — a list of
/// <c>action</c> (the <c>error.handle.Actions</c> recovery chain, etc.). The value's type is set
/// eagerly at read time (<c>actions</c>); this materializes the deferred bytes NATIVELY via STJ —
/// <c>actions.@this</c> is an <c>IList&lt;action&gt;</c>, so STJ builds the collection and each
/// action (module/action eager, <c>Parameters</c> deferring through the Wire). Then it stamps the
/// authored templates (the recovery chain is developer code, so its <c>%ref%</c> holes are live).
/// Replaces the old <c>actions</c> Convert hook + <c>action.FromWire</c> + <c>FromWireShape</c>.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        // The nested params read through the Wire (ReadOptions carries ctx.Template), so their
        // %ref% holes ride as live templates already — no manual stamping.
        var options = global::app.data.Wire.ReadOptions(ctx);
        return System.Text.Json.JsonSerializer
            .Deserialize<global::app.goal.steps.step.actions.@this>(reader.RawValue(), options)
            ?? new global::app.goal.steps.step.actions.@this();
    }
}
