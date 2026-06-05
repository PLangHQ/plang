using System.Text.Json.Serialization;

namespace app.data;

/// <summary>
/// The default Data JSON shape for every STJ path that does NOT register the channel
/// wire serializer in its options — clones, snapshots, debug dumps, error formatting,
/// any accidental reflection. It is <see cref="Wire"/> with two settings fixed:
/// <c>Sign=false</c> (a local serialization must never mint a signature or fire signing
/// I/O) and the <see cref="global::app.View.Store"/> view (an exact local copy keeps
/// every field).
///
/// <para>This lives as <c>Data</c>'s <c>[JsonConverter]</c> attribute so a Data serializes
/// as the canonical <c>{@schema, name, type, value, …}</c> shape <em>everywhere</em>, not
/// only on the channel — so it round-trips back to a Data (its <c>@schema</c> marker
/// recognized) instead of degrading to a marker-less map. The channel still signs: STJ
/// ranks a converter in <c>JsonSerializerOptions.Converters</c> above a type attribute, so
/// the per-actor options' signing <see cref="Wire"/> outranks this attribute on the wire
/// path. This attribute only takes effect where no Wire is registered.</para>
/// </summary>
public sealed class WireLocal : Wire
{
    public WireLocal() : base(global::app.View.Store, sign: false) { }
}
