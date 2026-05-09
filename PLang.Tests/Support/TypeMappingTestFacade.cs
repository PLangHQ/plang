// Test-only compatibility facade for the former `App.Utils.TypeMapping` and
// `App.Utils.Json` static classes. Production callers were migrated to
// `app.Types.X(...)` (stage 26) and the dispersed Json bag homes (stage 27);
// tests retain the flat static-call shape for ergonomics.

namespace App.Utils;

internal static class TypeMapping
{
    private static readonly global::App.@this _app = new(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-tests-typemapping"));

    /// <summary>For tests that need the live App backing this facade (e.g. Register that must persist across calls).</summary>
    internal static global::App.@this App => _app;

    public static System.Type? GetType(string typeName) => _app.Types.Get(typeName);

    public static string GetTypeName(System.Type type) => _app.Types.GetTypeName(type);

    public static void Register(string plangName, System.Type clrType) => _app.Types.Register(plangName, clrType);

    public static string[]? GetValidValues(System.Type type, global::App.Actor.Context.@this? context = null)
        => _app.Types.GetValidValues(type, context);

    public static List<string> GetBuilderTypeNames() => _app.Types.GetBuilderTypeNames();

    public static List<global::App.Modules.Schema.Entry> BuildTypeEntries(global::App.Modules.@this? modules)
        => _app.Types.BuildTypeEntries(modules);

    public static bool IsScalarPlangType(System.Type type) => global::App.Types.@this.IsScalarPlangType(type);

    public static bool IsPrimitive(System.Type type) => global::App.Types.@this.IsPrimitive(type);

    public static T? ConvertTo<T>(object? value) => global::App.Types.@this.ConvertTo<T>(value);

    public static object? ConvertTo(object? value, System.Type targetType) => global::App.Types.@this.ConvertTo(value, targetType);

    public static void Populate(object target, IDictionary<string, object?> values) => global::App.Types.@this.Populate(target, values);

    public static (object? Value, global::App.Errors.Error? Error) TryConvertTo(
        object? value, System.Type targetType, global::App.Actor.Context.@this? context = null)
        => global::App.Types.@this.TryConvertTo(value, targetType, context);
}

/// <summary>
/// Test-only facade for the former <c>App.Utils.TypeConverter</c> static class.
/// Stage 27 absorbed it into <see cref="global::App.Types.@this"/>; this facade routes back.
/// </summary>
internal static class TypeConverter
{
    public static T? ConvertTo<T>(object? value) => global::App.Types.@this.ConvertTo<T>(value);
    public static object? ConvertTo(object? value, System.Type targetType) => global::App.Types.@this.ConvertTo(value, targetType);
    public static void Populate(object target, IDictionary<string, object?> values) => global::App.Types.@this.Populate(target, values);
    public static (object? Value, global::App.Errors.Error? Error) TryConvertTo(
        object? value, System.Type targetType, global::App.Actor.Context.@this? context = null)
        => global::App.Types.@this.TryConvertTo(value, targetType, context);
}

/// <summary>
/// Test-only facade for the former <c>App.Utils.Json</c> static class. Each option bag
/// dispersed to its consumer in stage 27; this facade routes back to those homes so the
/// existing <c>using App.Utils;</c> in tests keeps resolving.
/// </summary>
internal static class Json
{
    public static System.Text.Json.JsonSerializerOptions CaseInsensitiveRead { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(allowIntegerValues: true), new global::App.Data.EmptyStringToNullEnumConverterFactory(), new global::App.Channels.Serializers.TimeSpanIso8601() },
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };

    public static System.Text.Json.JsonSerializerOptions CamelCaseIndented => global::App.@this.CamelCaseIndented;
    public static System.Text.Json.JsonSerializerOptions PrWrite => global::App.Builder.@this.PrWrite;
    public static System.Text.Json.JsonSerializerOptions DiagnosticOutput => global::App.Diagnostics.@this.Options;
}
