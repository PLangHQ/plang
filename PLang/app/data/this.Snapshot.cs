using System.Text.Json.Serialization;

namespace app.data;

/// <summary>
/// Data — snapshot/resume concern. Any action whose result Type satisfies
/// <c>Type.Exit()</c> MUST attach a non-null Snapshot here before returning.
/// Snapshot capture happens via <c>action.Snapshot()</c> while the Call frame
/// is still alive.
/// </summary>
public partial class @this
{
    // JsonIgnore: Snapshot is in-process state. Stateless-resume wire shape
    // is built by a per-channel serializer (architect's "Per-channel
    // serializer for stateless suspend" follow-up). Including it as a JSON
    // member here induces recursion via Variables→Data→Snapshot→Variables.
    [JsonIgnore]
    public snapshot.@this? Snapshot { get; set; }
}
