namespace App.Test;

/// <summary>
/// Test mode flag. When enabled, other systems can adjust behavior
/// (e.g., in-memory databases, assertion tracking).
/// Activated by: plang --test
/// </summary>
public sealed class @this
{
    public bool IsEnabled { get; set; }

    public @this(App.@this app) { }
}
