using System.Text.Json.Serialization.Metadata;

namespace App.Channels.Serializers;

/// <summary>
/// Always-on filter that strips properties marked with [Sensitive] from serialization output.
/// Applied to JsonStreamSerializer's default options so all output paths exclude sensitive data.
/// Storage (raw JsonSerializer via DataSource) does NOT use this filter — sensitive data persists.
/// </summary>
public static class SensitivePropertyFilter
{
    public static void Filter(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

        for (int i = typeInfo.Properties.Count - 1; i >= 0; i--)
        {
            var prop = typeInfo.Properties[i];
            if (prop.AttributeProvider?.IsDefined(typeof(SensitiveAttribute), false) == true)
                typeInfo.Properties.RemoveAt(i);
        }
    }
}
