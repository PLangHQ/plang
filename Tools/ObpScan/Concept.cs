namespace ObpScan;

/// <summary>Namespace-as-concept: two namespaces share a concept when one contains the other; the
/// short form drops the `app.` prefix to the last two segments. The one home for "what concept does
/// this namespace name" — every consumer asks here rather than string-slicing at the call site.</summary>
public static class Concept
{
    public static bool Same(string a, string b)
        => a == b || a.StartsWith(b) || b.StartsWith(a);

    public static string Short(string ns)
    {
        var parts = ns.Replace("app.", "").Split('.');
        return parts.Length <= 2 ? string.Join(".", parts) : string.Join(".", parts[^2..]);
    }
}
