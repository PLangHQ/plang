namespace PLang.Tests.App.Serialization;

/// <summary>A test-only item carrying a [Masked] property — the masking machinery's fixture: the key
/// rides the wire, the value writes "****" on the wire and its real value in storage.</summary>
public sealed class MaskedItem : global::app.type.item.@this
{
    [global::app.Out, global::app.Store] public string? key { get; init; }
    [global::app.Out, global::app.Masked, global::app.Store] public object? value { get; init; }

    /// <summary>A structural item — its tagged fields ride the wire.</summary>
    protected internal override System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
        => OutputTagged(writer);
}
