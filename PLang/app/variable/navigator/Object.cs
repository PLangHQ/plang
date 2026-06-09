using System.Reflection;

namespace app.variable.navigator;

/// <summary>
/// Navigates CLR objects via reflection. Lowest priority fallback.
/// </summary>
public sealed class Object : INavigator
{
    public bool CanNavigate(global::app.data.@this data) => data.Peek() != null;

    public async System.Threading.Tasks.ValueTask<global::app.data.@this> Navigate(global::app.data.@this data, string key)
    {
        var value = data.Value;
        if (value == null) return global::app.data.@this.NotFound(key);

        // Walk the inheritance chain bottom-up so properties shadowed in a
        // derived type win over the base — GetProperty(name, IgnoreCase) throws
        // AmbiguousMatchException when both declare the same name (e.g.
        // AssertionError.Variables shadows IError.Variables).
        PropertyInfo? prop = null;
        for (var t = value.GetType(); t != null && prop == null; t = t.BaseType)
        {
            prop = t.GetProperty(key,
                BindingFlags.Public | BindingFlags.Instance |
                BindingFlags.IgnoreCase | BindingFlags.DeclaredOnly);
        }
        if (prop == null) return global::app.data.@this.NotFound(key);

        try
        {
            var resolved = prop.GetValue(value);
            // Relay an already-Data property value rather than repackaging it
            // (Rule #7). A property like Error.Callback returns a Data<snapshot>;
            // wrapping it in another Data produces a Data-in-Data that serializes
            // as a double envelope. Hand back the Data itself.
            return resolved is data.@this d ? d : new data.@this(key, resolved, parent: data);
        }
        catch (TargetInvocationException ex)
        {
            return global::app.data.@this.FromError(new global::app.error.ServiceError(
                $"Failed to read '{key}': {(ex.InnerException ?? ex).Message}",
                "NavigationError", 500) { Exception = ex });
        }
    }
}
