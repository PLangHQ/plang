namespace App.Channels.Channel.Role;

/// <summary>
/// Logical role of a channel within an actor's I/O surface.
/// Every actor has one channel per role; custom-named channels carry no role.
/// </summary>
public enum @this
{
    /// <summary>No assigned role (custom-named channels).</summary>
    None,
    Output,
    Error,
    Input
}
