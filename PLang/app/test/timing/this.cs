using app.Attributes;

namespace app.test.timing;

/// <summary>
/// Wall-clock elapsed for a single top-level step in a test's entry goal. Holds
/// the <see cref="Step"/> itself (not a decomposed index) — index, text, and goal
/// are all reachable through the reference, single source of truth. A report names the
/// step (its index and text) and never writes its code: the code's %variables% would render
/// in the runner's context. Named "timing" via the @this namespace-tail convention.
/// </summary>
public sealed class @this : global::app.type.item.@this
{
    public required global::app.goal.step.@this Step { get; init; }

    /// <summary>The step, by its index in its goal.</summary>
    [Out] public int Index => Step.Index;

    /// <summary>The step as written.</summary>
    [Out] public string Text => Step.Text;

    [Out] public required global::app.type.item.duration.@this Elapsed { get; init; }

    /// <summary>Self-write: a structural item — its tagged [Out] fields ride the wire.</summary>
    public override System.Threading.Tasks.ValueTask Output(
        global::app.channel.serializer.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context)
        => OutputTagged(writer, mode, context);
}
