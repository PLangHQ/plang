using App.modules;

namespace App.Types;

/// <summary>
/// Owns PLang name ↔ CLR type identity. Delegates to <see cref="Utils.TypeMapping"/>
/// (static, shared with the source generator and v1 code).
///
/// File-format characteristics (extension → Kind, extension → MIME, Kind →
/// compressibility) live separately on <see cref="App.Formats.@this"/> at
/// <c>app.Formats</c> — see stage 18.
/// </summary>
public sealed class @this
{
    // --- Name ↔ CLR type (delegates to TypeMapping — single source of truth) ---

    /// <summary>
    /// PLang type name → CLR type. Delegates to TypeMapping.GetType().
    /// </summary>
    public System.Type? Clr(string plangName) => Utils.TypeMapping.GetType(plangName);

    /// <summary>
    /// MIME content-type → CLR type for deserialization. Returns <c>null</c>
    /// when the input isn't a MIME string (no slash) or isn't a recognised
    /// family. Static — pure logic with no instance state — so it's reachable
    /// from <see cref="Utils.TypeMapping.GetType"/>'s static path too.
    /// </summary>
    public static System.Type? ClrFromMime(string mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType) || !mimeType.Contains('/')) return null;

        if (mimeType.StartsWith("text/", System.StringComparison.OrdinalIgnoreCase))
            return typeof(string);
        if (mimeType.StartsWith("image/", System.StringComparison.OrdinalIgnoreCase)
            || mimeType.StartsWith("audio/", System.StringComparison.OrdinalIgnoreCase)
            || mimeType.StartsWith("video/", System.StringComparison.OrdinalIgnoreCase))
            return typeof(byte[]);
        if (mimeType.Equals("application/json", System.StringComparison.OrdinalIgnoreCase))
            return typeof(object);
        if (mimeType.Equals("application/plang-goal", System.StringComparison.OrdinalIgnoreCase))
            return typeof(App.Goals.Goal.@this);
        if (mimeType.Equals("application/octet-stream", System.StringComparison.OrdinalIgnoreCase))
            return typeof(byte[]);

        return null;
    }

    /// <summary>
    /// CLR type → PLang type name. Delegates to TypeMapping.GetTypeName().
    /// </summary>
    public string Name(System.Type clrType) => Utils.TypeMapping.GetTypeName(clrType);

    /// <summary>
    /// Registers a domain type. Delegates to TypeMapping.Register().
    /// </summary>
    public void Register(string plangName, System.Type clrType) => Utils.TypeMapping.Register(plangName, clrType);

    /// <summary>
    /// Registers domain types needed for settings store rehydration.
    /// Called by App constructor. Each module's types are registered here.
    /// </summary>
    public void RegisterDomainTypes()
    {
        Register("identitydata", typeof(modules.identity.Identity));
    }

    /// <summary>
    /// Returns canonical builder type names. Delegates to TypeMapping.GetBuilderTypeNames().
    /// </summary>
    public List<string> BuilderNames() => Utils.TypeMapping.GetBuilderTypeNames();

    /// <summary>
    /// Returns the catalog's record/enum entries, keyed by name. Thin wrapper
    /// around TypeMapping.BuildTypeEntries(modules) for callers that want a dictionary view.
    /// </summary>
    public Dictionary<string, App.Modules.Schema.Entry> ComplexSchemas() =>
        Utils.TypeMapping.BuildTypeEntries(null).ToDictionary(e => e.Name, e => e);

    /// <summary>
    /// Gets valid values for a constrained type. Delegates to TypeMapping.GetValidValues().
    /// </summary>
    public static string[]? ValidValues(System.Type type) => Utils.TypeMapping.GetValidValues(type);
}
