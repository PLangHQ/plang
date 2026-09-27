// Test-only compatibility facade for the former `App.Utils.TypeMapping` and
// `App.Utils.Json` static classes. Production callers were migrated to
// `app.type.list.X(...)` (stage 26) and the dispersed Json bag homes (stage 27);
// tests retain the flat static-call shape for ergonomics.

namespace app.Utils;

internal static class TypeMapping
{
    private static readonly global::app.@this _app = new(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-tests-typemapping"));

    /// <summary>For tests that need the live App backing this facade (e.g. Register that must persist across calls).</summary>
    internal static global::app.@this App => _app;

    public static System.Type? GetType(string typeName) => global::PLang.Tests.TypeListExtensions.Clr(_app.type.list, typeName);

    /// <summary>The face of the entity the CLR type names — what a catalog prints.</summary>
    public static string GetTypeName(System.Type type) => _app.type.list[type].ToString();

    public static void Register(string plangName, System.Type clrType) => _app.type.list.Add(clrType, _app.User.Context, plangName);

    /// <summary>The options of the closed set <paramref name="type"/> draws from, or null when it has none.</summary>
    /// A nullable, a <c>data&lt;T&gt;</c> or a <c>choice&lt;T&gt;</c> reads through to the set it holds.
    public static IReadOnlyList<string>? Values(System.Type type)
    {
        var held = System.Nullable.GetUnderlyingType(type) ?? type;
        if (held.IsGenericType && held.GetGenericTypeDefinition() == typeof(global::app.data.@this<>))
            held = held.GetGenericArguments()[0];
        if (held.IsGenericType && held.GetGenericTypeDefinition() == typeof(global::app.type.item.choice.@this<>))
            held = held.GetGenericArguments()[0];
        var closed = held.IsEnum || held.GetMethod("Choices", System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy) != null;
        return closed && new global::app.type.item.choice.set.@this(held) is { IsClosed: true } set ? set.Values : null;
    }
}

/// <summary>
/// Test-only facade for the former <c>App.Utils.Json</c> static class. The
/// option bags now live with their consumers; this facade routes back to
/// those homes so the existing <c>using app.Utils;</c> in tests keeps resolving.
/// </summary>
internal static class Json
{
    // Test-only: several suites hand-write a goal to a .pr file for the runtime to load.
    // Production's App.Save moved off this bag to the clr + SerializeItemAsync path (W7); the bag
    // lives here now, owned by its only remaining (test) callers. Keeps the path converter so a
    // goal's path fields serialize scheme-correct (goals carry no TimeSpan, so no TimeSpan converter).
    public static System.Text.Json.JsonSerializerOptions CamelCaseIndented { get; } = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters =
        {
            new global::app.type.format.json.Converter(),
        },
    };

    public static System.Text.Json.JsonSerializerOptions DiagnosticOutput => global::app.Diagnostics.Format.Options;
}
