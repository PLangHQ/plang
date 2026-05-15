using System.Reflection;
using System.Text.Json.Serialization.Metadata;
using app;

namespace app.Channels.Serializers.Filters;

/// <summary>
/// Filters JSON properties based on view attributes.
///
/// For types that use view attributes (any property has [Store], [LlmBuilder], [Debug], or [Default]):
///   - Only properties tagged with the active view are serialized
///   - Properties with no view attribute are excluded
///
/// For types that don't use view attributes: no filtering, normal serialization.
/// </summary>
public static class View
{
    private static readonly Type[] ViewAttributeTypes =
    {
        typeof(StoreAttribute),
        typeof(LlmBuilderAttribute),
        typeof(DebugAttribute),
        typeof(DefaultAttribute)
    };

    private static readonly Dictionary<global::app.View, Type> ViewToAttribute = new()
    {
        [global::app.View.Store] = typeof(StoreAttribute),
        [global::app.View.LlmBuilder] = typeof(LlmBuilderAttribute),
        [global::app.View.Debug] = typeof(DebugAttribute),
        [global::app.View.Default] = typeof(DefaultAttribute),
    };

    public static Action<JsonTypeInfo> For(global::app.View view) => typeInfo =>
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object) return;
        if (!ViewToAttribute.TryGetValue(view, out var targetAttr)) return;

        // Check if this type uses view attributes at all
        bool isViewAware = false;
        foreach (var prop in typeInfo.Properties)
        {
            if (prop.AttributeProvider != null && HasAnyViewAttribute(prop.AttributeProvider))
            {
                isViewAware = true;
                break;
            }
        }

        if (!isViewAware) return;

        // Only keep properties tagged with the target view
        for (int i = typeInfo.Properties.Count - 1; i >= 0; i--)
        {
            var prop = typeInfo.Properties[i];
            if (prop.AttributeProvider == null ||
                !prop.AttributeProvider.IsDefined(targetAttr, false))
            {
                typeInfo.Properties.RemoveAt(i);
            }
        }
    };

    private static bool HasAnyViewAttribute(ICustomAttributeProvider provider)
    {
        foreach (var attrType in ViewAttributeTypes)
        {
            if (provider.IsDefined(attrType, false))
                return true;
        }
        return false;
    }
}
