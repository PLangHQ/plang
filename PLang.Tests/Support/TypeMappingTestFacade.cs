// Test-only compatibility facade for the former `App.Utils.TypeMapping` and
// `App.Utils.Json` static classes. Production callers were migrated to
// `app.types.X(...)` (stage 26) and the dispersed Json bag homes (stage 27);
// tests retain the flat static-call shape for ergonomics.

namespace app.Utils;

internal static class TypeMapping
{
    private static readonly global::app.@this _app = new(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-tests-typemapping"));

    /// <summary>For tests that need the live App backing this facade (e.g. Register that must persist across calls).</summary>
    internal static global::app.@this App => _app;

    public static System.Type? GetType(string typeName) => _app.Types.Get(typeName);

    public static string GetTypeName(System.Type type) => _app.Types.GetTypeName(type);

    public static void Register(string plangName, System.Type clrType) => _app.Types.Register(plangName, clrType);

    public static string[]? GetValidValues(System.Type type, global::app.actor.context.@this? context = null)
        => _app.Types.GetValidValues(type, context);

    public static List<string> GetBuilderTypeNames() => _app.Types.GetBuilderTypeNames();

    public static List<global::app.Modules.Schema.Entry> BuildTypeEntries(global::app.Modules.@this? modules)
        => _app.Types.BuildTypeEntries(modules);

    public static bool IsScalarPlangType(System.Type type) => global::app.types.@this.IsScalarPlangType(type);

    public static bool IsPrimitive(System.Type type) => global::app.types.@this.IsPrimitive(type);

    public static T? ConvertTo<T>(object? value) => global::app.types.@this.ConvertTo<T>(value);

    public static object? ConvertTo(object? value, System.Type targetType) => global::app.types.@this.ConvertTo(value, targetType);

    public static void Populate(object target, IDictionary<string, object?> values) => global::app.types.@this.Populate(target, values);

    public static (object? Value, global::app.errors.Error? Error) TryConvertTo(
        object? value, System.Type targetType, global::app.actor.context.@this? context = null)
        => global::app.types.@this.TryConvertTo(value, targetType, context);
}

/// <summary>
/// Test-only facade for the former <c>App.Utils.TypeConverter</c> static class.
/// Stage 27 absorbed it into <see cref="global::app.types.@this"/>; this facade routes back.
/// </summary>
internal static class TypeConverter
{
    public static T? ConvertTo<T>(object? value) => global::app.types.@this.ConvertTo<T>(value);
    public static object? ConvertTo(object? value, System.Type targetType) => global::app.types.@this.ConvertTo(value, targetType);
    public static void Populate(object target, IDictionary<string, object?> values) => global::app.types.@this.Populate(target, values);
    public static (object? Value, global::app.errors.Error? Error) TryConvertTo(
        object? value, System.Type targetType, global::app.actor.context.@this? context = null)
        => global::app.types.@this.TryConvertTo(value, targetType, context);
}

/// <summary>
/// Test-only facade for the former <c>App.Utils.Json</c> static class. Each option bag
/// dispersed to its consumer in stage 27; this facade routes back to those homes so the
/// existing <c>using App.Utils;</c> in tests keeps resolving.
/// </summary>
internal static class Json
{
    // Routes to the production conversion-path bag (via internal accessor on
    // App.Types.@this). Keeps the test surface pinned to one production source so a
    // converter added in Conversion.cs is exercised by tests automatically. The
    // http/code/Default bag is independent and not covered here — by design.
    public static System.Text.Json.JsonSerializerOptions CaseInsensitiveRead => global::app.types.@this.CaseInsensitiveRead;

    public static System.Text.Json.JsonSerializerOptions CamelCaseIndented => global::app.@this.CamelCaseIndented;
    public static System.Text.Json.JsonSerializerOptions PrWrite => global::app.Builder.@this.PrWrite;
    public static System.Text.Json.JsonSerializerOptions DiagnosticOutput => global::app.Diagnostics.Format.Options;
}
