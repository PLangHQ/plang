namespace app.type.item.wire;

/// <summary>
/// A still-encoded slice the builder marked a template — a <c>.pr</c> row's container holding
/// <c>%variables%</c>, with the variables its row lists. Its slice names the variables, not their values, so
/// outside what plang keeps it is no token of any format: it is decoded, and its parts write themselves (each
/// renders its variable). What plang keeps (a <c>.pr</c>, the Store view) holds it as authored. Read and lowered
/// as any wire is.
/// </summary>
public sealed class template : @this
{
    public template(string slice, global::app.type.@this type, kind.plang.@this reader,
        IReadOnlyList<global::app.type.item.variable.@this> variable, bool built = false)
        : base(slice, type, reader, variable, built) { }

    // A template renders with the caller's context: there is no context-free write of it.
    public override void Write(global::app.type.format.IWriter w)
        => throw new System.InvalidOperationException(
            "a template writes through Output(writer, mode, context) — rendering needs the caller's context.");

    public override async System.Threading.Tasks.ValueTask Output(
        global::app.type.format.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context)
    {
        // The Store view keeps what was authored: a .pr holds the template's slice as written, a wire's own
        // relay. (The same check as source.Output's; where the face is chosen is the writer-rule sweep's.)
        if (mode == global::app.View.Store)
        {
            await base.Output(writer, mode, context);
            return;
        }
        if (context is null) throw new System.InvalidOperationException(
            "a template writes only with the caller's context — rendering needs it.");
        await Decoded(context).Output(writer, mode, context);
    }
}
