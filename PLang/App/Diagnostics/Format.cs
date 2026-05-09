using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace App.Diagnostics;

/// <summary>
/// Diagnostic-output subsystem — formats values for human-readable diagnostic strings
/// (assertion failure messages, console test output, error reports). Scalars render directly;
/// strings get quoted; anything else goes through a JsonSerializer that masks
/// <see cref="App.Attributes.SensitiveAttribute"/>-marked properties as "******" so the
/// data shape is preserved while secrets are redacted.
///
/// Distinct from storage (keeps sensitive data) and user output (strips it entirely).
/// Stage 27 absorbed this from <c>Utils.Json.DiagnosticOutput</c> + <c>FormatForDiagnostic</c>.
///
/// Static class because all three consumers (Tester reports, AssertionError messages,
/// modules/assert) call from static contexts where no App is in scope. The state held is
/// a single immutable JsonSerializerOptions — Rule C exception class for pure-config bags
/// with no instance variation. Not named <c>@this</c> because there is no
/// <c>app.Diagnostics</c> mount; the folder-as-instance signal would be misleading.
/// </summary>
public static class Format
{
    private static readonly JsonSerializerOptions DiagnosticOutput = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { App.Channels.Serializers.Filters.Sensitive.Mask }
        }
    };

    /// <summary>
    /// Formats a value for inclusion in a human-readable diagnostic string. Scalars
    /// render directly; strings get quoted; anything else goes through the masked
    /// JsonSerializer. Never falls back to <c>value.ToString()</c> on arbitrary objects —
    /// that bypasses the mask (a C# record's auto-ToString prints every field).
    /// </summary>
    public static string Value(object? value)
    {
        if (value == null) return "(null)";
        if (value is string s) return $"\"{s}\"";
        var type = value.GetType();
        if (type.IsPrimitive || value is decimal || value is DateTime
            || value is DateTimeOffset || value is TimeSpan || value is Guid
            || value is Enum)
            return value.ToString() ?? "(null)";
        try { return JsonSerializer.Serialize(value, DiagnosticOutput); }
        catch { return type.Name; }
    }

    /// <summary>
    /// Direct access to the underlying JsonSerializerOptions for callers that need to
    /// drive the serializer themselves (e.g. test-report envelope serialization). Prefer
    /// <see cref="Value"/> when the result is a single value.
    /// </summary>
    public static JsonSerializerOptions Options => DiagnosticOutput;
}
