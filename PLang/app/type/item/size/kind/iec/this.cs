namespace app.type.item.size.kind.iec;

/// <summary>IEC 80000-13 — powers of 1024: <c>500 KiB</c>, <c>95.4 MiB</c>, <c>2 GiB</c>.</summary>
public sealed class @this : kind.@this
{
    public @this() : base("iec", 1024, "KiB", "MiB", "GiB", "TiB", "PiB") { }
}
