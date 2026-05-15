namespace app.Config;

/// <summary>
/// Marker interface for strongly typed module settings.
/// Classes implementing IConfig are detected by the source generator, which produces:
/// 1. Scope-aware property bodies (read side) — each property resolves from the settings scope chain
/// 2. A settings action handler (write side) — [Action("settings")] in the module namespace
/// 3. A settings manifest entry (for builder discovery)
///
/// Example:
///   public partial class Settings : IConfig
///   {
///       public long Max { get; set; } = 100 * 1024 * 1024;
///   }
/// </summary>
public interface IConfig
{
}
