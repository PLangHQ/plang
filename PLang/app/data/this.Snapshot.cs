using app.type.list;
using System.Text.Json.Serialization;

namespace app.data;

/// <summary>
/// Data — snapshot/resume concern. Any action whose result Type satisfies
/// <c>Exits</c> MUST attach a non-null Snapshot here before returning.
/// Snapshot capture happens via <c>action.Snapshot()</c> while the Call frame
/// is still alive.
/// </summary>
public partial class @this
{
    // JsonIgnore: Snapshot is in-process state; no wire shape carries it.
    // Including it as a JSON member here induces recursion via
    // Variables→Data→Snapshot→Variables.
    [JsonIgnore]
    public snapshot.@this? Snapshot { get; set; }
}
