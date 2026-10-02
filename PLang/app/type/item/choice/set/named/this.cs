using System.Reflection;

namespace app.type.item.choice.set.named;

/// <summary>A closed set a class names itself: a static <c>Choices(context?)</c> lists the options, and its
/// <c>ctor(string)</c> makes the member a symbol names (refusing one that is none of them).</summary>
public sealed class @this : set.@this
{
    private readonly System.Type _clr;
    private readonly MethodInfo _choices;

    internal @this(System.Type clr, MethodInfo choices) : base(clr, Declared(clr))
    {
        _clr = clr;
        _choices = choices;
    }

    /// <summary>The <c>Choices(context?)</c> a class declares; null when it declares none.</summary>
    internal static MethodInfo? Choices(System.Type clr)
        => clr.GetMethod("Choices", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

    public override System.Collections.Generic.IReadOnlyList<string> Values
    {
        get
        {
            object?[] args = _choices.GetParameters().Length == 1 ? new object?[] { null } : System.Array.Empty<object?>();
            return _choices.Invoke(null, args) switch
            {
                string[] arr => arr,
                System.Collections.Generic.IReadOnlyList<string> list => list,
                System.Collections.Generic.IEnumerable<string> seq => new System.Collections.Generic.List<string>(seq),
                _ => System.Array.Empty<string>(),
            };
        }
    }

    public override object Member(string symbol)
    {
        var made = _clr.GetConstructor(new[] { typeof(string) })
                   ?? throw new System.InvalidOperationException($"choice<{_clr.Name}>: a named set makes its member by a ctor(string).");
        try { return made.Invoke(new object?[] { symbol }); }
        catch (TargetInvocationException ex) when (ex.InnerException is System.ArgumentException refused) { throw refused; }
    }
}
