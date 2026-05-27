using System.Text;
using app.Attributes;

namespace app.data;

/// <summary>
/// Translatable string with PLang variable resolution.
/// Resolves %var% placeholders via a resolver function.
/// Translation lookup is deferred to Phase 6+.
/// </summary>
[PlangType]
public sealed class TString
{
    /// <summary>Catalog example — read via reflection by the schema builder.</summary>
    public static string Example => "Hello %name%";

    /// <summary>The raw template string with %var% placeholders.</summary>
    public string Value { get; }

    /// <summary>Translation key for lookup (future use).</summary>
    public string? Key { get; init; }

    /// <summary>
    /// Resolves a variable name (without %) to its value.
    /// Backed by Variables when created at runtime.
    /// </summary>
    private readonly Func<string, object?>? _resolver;

    public TString(string value, string? key = null, Func<string, object?>? resolver = null)
    {
        Value = value;
        Key = key;
        _resolver = resolver;
    }

    /// <summary>
    /// Returns the string with all %var% placeholders resolved.
    /// If no resolver is set, returns the raw Value.
    /// </summary>
    public override string ToString()
    {
        if (_resolver == null || string.IsNullOrEmpty(Value) || !Value.Contains('%'))
            return Value;

        return Resolve(Value, _resolver);
    }

    /// <summary>
    /// Resolves %var% placeholders in a template string using the given resolver.
    /// Supports nested dot notation: %user.name%, %items[0]%.
    /// Unresolved variables are left as-is.
    /// </summary>
    internal static string Resolve(string template, Func<string, object?> resolver)
    {
        var sb = new StringBuilder(template.Length);
        int i = 0;

        while (i < template.Length)
        {
            int start = template.IndexOf('%', i);
            if (start < 0)
            {
                sb.Append(template, i, template.Length - i);
                break;
            }

            // Append text before the %
            sb.Append(template, i, start - i);

            int end = template.IndexOf('%', start + 1);
            if (end < 0)
            {
                // No closing % — append the rest as-is
                sb.Append(template, start, template.Length - start);
                break;
            }

            var varName = template.Substring(start + 1, end - start - 1);
            if (string.IsNullOrWhiteSpace(varName))
            {
                // Empty %% — keep literal
                sb.Append("%%");
            }
            else
            {
                var resolved = resolver(varName);
                if (resolved != null)
                    sb.Append(resolved);
                else
                    sb.Append('%').Append(varName).Append('%'); // unresolved → keep original
            }

            i = end + 1;
        }

        return sb.ToString();
    }

    public static implicit operator TString(string value) => new(value);
    public static implicit operator string(TString ts) => ts.ToString();

    public override bool Equals(object? obj) => obj switch
    {
        TString other => string.Equals(Value, other.Value, StringComparison.Ordinal),
        string str => string.Equals(Value, str, StringComparison.Ordinal),
        _ => false
    };

    public override int GetHashCode() => Value.GetHashCode();
}
