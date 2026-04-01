using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory.Navigators;

namespace PLang.Runtime2.Engine.Memory;

/// <summary>
/// Data — navigation concern.
/// GetChild traverses dot notation and bracket indexing into nested values.
/// </summary>
public partial class Data
{
    private const int MaxNavigationDepth = 100;

    /// <summary>
    /// Gets a child value by path (dot notation or index).
    /// </summary>
    public virtual Data? GetChild(string path, int depth = 0)
    {
        if (string.IsNullOrEmpty(path))
            return this;

        if (depth > MaxNavigationDepth)
            return FromError(new ServiceError(
                $"Navigation path exceeds maximum depth ({MaxNavigationDepth})",
                "NavigationDepthExceeded", 400));

        // Handle dot notation
        var dotIndex = path.IndexOf('.');
        var bracketIndex = path.IndexOf('[');

        string segment;
        string remaining;

        if (dotIndex >= 0 && (bracketIndex < 0 || dotIndex < bracketIndex))
        {
            segment = path[..dotIndex];
            remaining = path[(dotIndex + 1)..];
        }
        else if (bracketIndex >= 0)
        {
            if (bracketIndex > 0)
            {
                segment = path[..bracketIndex];
                remaining = path[bracketIndex..];
            }
            else
            {
                var closeBracket = path.IndexOf(']');
                if (closeBracket < 0)
                    return null;
                segment = path[1..closeBracket];
                remaining = closeBracket + 1 < path.Length ? path[(closeBracket + 1)..].TrimStart('.') : "";
            }
        }
        else
        {
            segment = path;
            remaining = "";
        }

        // Get child value from current value
        var childValue = GetChildValue(segment);
        if (childValue == null)
            return null;

        var child = new Data(segment, childValue, parent: this);
        child.Context = _context;

        // Inject context on IContext values during traversal
        if (childValue is PLang.Runtime2.modules.IContext contextual && _context != null)
            contextual.Context = _context;

        if (string.IsNullOrEmpty(remaining))
            return child;

        return child.GetChild(remaining, depth + 1);
    }

    private object? GetChildValue(string key)
    {
        // First check properties on the Data object itself (e.g., PathData.Exists)
        var ownProp = GetType().GetProperty(key,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
        if (ownProp != null && (ownProp.DeclaringType != typeof(Data)
            || key.Equals("Success", StringComparison.OrdinalIgnoreCase)
            || key.Equals("Error", StringComparison.OrdinalIgnoreCase)))
        {
            var v = ownProp.GetValue(this);
            Console.WriteLine($"[Nav.{key}] = {v} type={GetType().Name}");
            return v;
        }

        var val = Value;
        if (val == null) return null;
        return ValueNavigators.Navigate(val, key);
    }
}
