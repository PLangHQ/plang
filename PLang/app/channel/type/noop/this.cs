namespace app.channel.type.noop;

/// <summary>
/// /dev/null channel — the fallback returned by <see cref="app.channel.list.@this.Channel"/>
/// when no channel is registered under the requested name. Writes complete
/// successfully and drop the payload; reads return NotFound; asks are no-ops.
///
/// <para>
/// Exists so call sites (build-time warnings, runtime advisory writes) don't
/// need to null-check the channel lookup. A standalone-callable
/// <c>IClass.Build()</c> that writes a warning outside an active build still
/// finds a sink — it just goes nowhere, no observers fire.
/// </para>
/// </summary>
public sealed class @this : global::app.channel.@this
{
    public @this(string name)
    {
        Name = name;
        Direction = ChannelDirection.Bidirectional;
    }

    public override System.Threading.Tasks.Task<global::app.data.@this> Write(
        global::app.data.@this data, System.Threading.CancellationToken ct = default)
        => System.Threading.Tasks.Task.FromResult(global::app.data.@this.Ok());

    public override System.Threading.Tasks.Task<global::app.data.@this> Read(
        System.Threading.CancellationToken ct = default)
        => System.Threading.Tasks.Task.FromResult(global::app.data.@this.NotFound());

    public override System.Threading.Tasks.Task<global::app.data.@this> Ask(
        global::app.module.output.ask action, System.Threading.CancellationToken ct = default)
        => System.Threading.Tasks.Task.FromResult(global::app.data.@this.Ok());
}
