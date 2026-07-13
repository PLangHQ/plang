// Test-only compatibility facade for the former `App.Utils.TypeMapping` and
// `App.Utils.Json` static classes. Production callers were migrated to
// `app.Type.X(...)` (stage 26) and the dispersed Json bag homes (stage 27);
// tests retain the flat static-call shape for ergonomics.

namespace app.Utils;

internal static class TypeMapping
{
    private static readonly global::app.@this _app = new(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-tests-typemapping"));

    /// <summary>For tests that need the live App backing this facade (e.g. Register that must persist across calls).</summary>
    internal static global::app.@this App => _app;

    public static System.Type? GetType(string typeName) => _app.Type.Get(typeName);

    public static string GetTypeName(System.Type type) => _app.Type.GetTypeName(type);

    public static void Register(string plangName, System.Type clrType) => _app.Type.Register(plangName, clrType);

    public static string[]? GetValidValues(System.Type type, global::app.actor.context.@this? context = null)
        => _app.Type.GetValidValues(type, context);

    public static List<string> GetBuilderTypeNames() => _app.Type.GetBuilderTypeNames();

    public static List<global::app.type.@this> BuildTypeEntries(global::app.module.@this? modules)
        => _app.Type.BuildTypeEntries(modules);

    public static bool IsScalarPlangType(System.Type type) => global::app.type.list.@this.IsScalarPlangType(type);

    public static bool IsPrimitive(System.Type type) => global::app.type.list.@this.IsPrimitive(type);
}

/// <summary>
/// Test-only facade for the former <c>App.Utils.Json</c> static class. The
/// option bags now live with their consumers; this facade routes back to
/// those homes so the existing <c>using app.Utils;</c> in tests keeps resolving.
/// </summary>
internal static class Json
{
    // The shared STJ read options — the one production source (path/enum/timespan
    // converters, case-insensitive). A test needing the dict/record read set routes here.
    public static System.Text.Json.JsonSerializerOptions CaseInsensitiveRead => global::app.channel.serializer.json.Options.Read();

    public static System.Text.Json.JsonSerializerOptions CamelCaseIndented => global::app.@this.CamelCaseIndented;
    public static System.Text.Json.JsonSerializerOptions DiagnosticOutput => global::app.Diagnostics.Format.Options;
}
