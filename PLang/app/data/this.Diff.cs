using System.Text.Json;
using System.Text.Json.Serialization;

namespace app.data;

/// <summary>
/// Data — comparison concern.
/// Structural comparison of two Data objects via their JSON representations.
/// Used by the eval runner to compare .pr output against .golden files.
/// </summary>
public partial class @this
{
    // Per-Data static — pure config bag, no instance variation, allocation efficiency
    // matters because Data is allocated frequently. Stage 27 disperse-from-Json target.
    private static readonly JsonSerializerOptions _camelCaseIndented = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new global::app.channel.serializer.TimeSpanIso8601() }
    };
    /// <summary>
    /// Structural golden-file diff: serializes both Data to JSON and walks the trees.
    /// Returns a Data whose Value is a dictionary with match result, field-level diffs,
    /// and lists of missing/extra fields. Distinct from <see cref="Compare"/> — that is
    /// the typed value comparison returning <see cref="Comparison"/>; this is the eval
    /// runner's report shape.
    /// </summary>
    public @this Diff(@this other)
    {
        var thisJson = SerializeForComparison(this);
        var otherJson = SerializeForComparison(other);

        using var thisDoc = JsonDocument.Parse(thisJson);
        using var otherDoc = JsonDocument.Parse(otherJson);

        var diff = CompareElements(thisDoc.RootElement, otherDoc.RootElement, "");

        return new @this("comparison", diff);
    }

    private static string SerializeForComparison(@this data)
    {
        // Peek() is statically typed `item.@this`; serializing it directly would
        // bind STJ to the abstract base's contract (its infra properties) — identical
        // for every value, so all comparisons would match. Cast to object so STJ uses
        // the runtime type and the value's own [JsonConverter] (text→"hello", etc.).
        return JsonSerializer.Serialize((object?)data.Peek(), _camelCaseIndented);
    }

    private static Dictionary<string, object?> CompareElements(
        JsonElement expected, JsonElement actual, string path)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (expected.ValueKind != actual.ValueKind)
        {
            // Treat null and missing (Undefined) as equivalent
            if (IsNullish(expected) && IsNullish(actual))
            {
                result["match"] = true;
                return result;
            }

            result["match"] = false;
            result["expected"] = expected.ToString();
            result["actual"] = actual.ToString();
            result["expectedKind"] = expected.ValueKind.ToString();
            result["actualKind"] = actual.ValueKind.ToString();
            return result;
        }

        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                return CompareObjects(expected, actual, path);

            case JsonValueKind.Array:
                return CompareArrays(expected, actual, path);

            case JsonValueKind.String:
                var expStr = expected.GetString();
                var actStr = actual.GetString();
                result["match"] = string.Equals(expStr, actStr, StringComparison.Ordinal);
                if (!(bool)result["match"]!)
                {
                    result["expected"] = expStr;
                    result["actual"] = actStr;
                }
                return result;

            case JsonValueKind.Number:
                // Compare as decimal to avoid int/long/double boxing mismatches
                var expNum = expected.GetDecimal();
                var actNum = actual.GetDecimal();
                result["match"] = expNum == actNum;
                if (!(bool)result["match"]!)
                {
                    result["expected"] = expNum;
                    result["actual"] = actNum;
                }
                return result;

            case JsonValueKind.True:
            case JsonValueKind.False:
                result["match"] = expected.GetBoolean() == actual.GetBoolean();
                return result;

            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                result["match"] = true;
                return result;

            default:
                result["match"] = string.Equals(
                    expected.GetRawText(), actual.GetRawText(), StringComparison.Ordinal);
                return result;
        }
    }

    private static Dictionary<string, object?> CompareObjects(
        JsonElement expected, JsonElement actual, string path)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var missingFields = new List<string>();
        var extraFields = new List<string>();
        var allMatch = true;

        // Index actual properties for case-insensitive lookup
        var actualProps = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in actual.EnumerateObject())
            actualProps[prop.Name] = prop.Value;

        // Check each expected property
        foreach (var prop in expected.EnumerateObject())
        {
            var fieldPath = string.IsNullOrEmpty(path) ? prop.Name : $"{path}.{prop.Name}";

            if (actualProps.TryGetValue(prop.Name, out var actualValue))
            {
                var fieldDiff = CompareElements(prop.Value, actualValue, fieldPath);
                fields[prop.Name] = fieldDiff;
                if (fieldDiff.TryGetValue("match", out var m) && m is bool match && !match)
                    allMatch = false;
                actualProps.Remove(prop.Name);
            }
            else
            {
                // Missing in actual — but treat null expected as matching missing
                if (IsNullish(prop.Value))
                {
                    fields[prop.Name] = new Dictionary<string, object?> { ["match"] = true };
                }
                else
                {
                    missingFields.Add(fieldPath);
                    allMatch = false;
                }
            }
        }

        // Check for extra properties in actual
        foreach (var (name, value) in actualProps)
        {
            var fieldPath = string.IsNullOrEmpty(path) ? name : $"{path}.{name}";
            // Extra null/undefined fields don't count as differences
            if (IsNullish(value))
                continue;
            extraFields.Add(fieldPath);
            allMatch = false;
        }

        result["match"] = allMatch;
        result["fields"] = fields;
        if (missingFields.Count > 0) result["missingFields"] = missingFields;
        if (extraFields.Count > 0) result["extraFields"] = extraFields;

        return result;
    }

    private static Dictionary<string, object?> CompareArrays(
        JsonElement expected, JsonElement actual, string path)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var allMatch = true;
        var items = new List<object?>();

        var expLen = expected.GetArrayLength();
        var actLen = actual.GetArrayLength();

        var maxLen = Math.Max(expLen, actLen);
        for (var i = 0; i < maxLen; i++)
        {
            var itemPath = $"{path}[{i}]";
            if (i >= expLen)
            {
                items.Add(new Dictionary<string, object?>
                {
                    ["match"] = false,
                    ["error"] = "extra item in actual",
                    ["actual"] = actual[i].ToString()
                });
                allMatch = false;
            }
            else if (i >= actLen)
            {
                items.Add(new Dictionary<string, object?>
                {
                    ["match"] = false,
                    ["error"] = "missing item in actual",
                    ["expected"] = expected[i].ToString()
                });
                allMatch = false;
            }
            else
            {
                var itemDiff = CompareElements(expected[i], actual[i], itemPath);
                items.Add(itemDiff);
                if (itemDiff.TryGetValue("match", out var m) && m is bool match && !match)
                    allMatch = false;
            }
        }

        result["match"] = allMatch;
        result["items"] = items;
        if (expLen != actLen)
        {
            result["expectedLength"] = expLen;
            result["actualLength"] = actLen;
        }

        return result;
    }

    private static bool IsNullish(JsonElement element) =>
        element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;
}
