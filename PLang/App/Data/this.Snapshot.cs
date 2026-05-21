using System.Text.Json.Serialization;

namespace App.Data;

/// <summary>
/// Data — snapshot/resume concern. Any action whose result Type satisfies
/// <c>Type.Exit()</c> MUST attach a non-null Snapshot here before returning.
/// Snapshot capture happens via <c>action.Snapshot()</c> while the Call frame
/// is still alive.
/// </summary>
public partial class @this
{
    [JsonInclude]
    public Snapshot.@this? Snapshot { get; set; }
}
