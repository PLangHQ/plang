using System.Reflection;

namespace app.type.item.choice.set;

/// <summary>
/// A closed set — the options a <c>choice</c> may take. The set is a CLR enum (its members) or a
/// class declaring a static <c>Choices(context?)</c>. Its plang name is its own declaration
/// (<c>[PlangType("operator")]</c>) and is the KIND of every choice drawn from it:
/// <c>{choice, kind: operator}</c>. Never a type of its own.
/// </summary>
public sealed class @this
{
    private readonly System.Type _clr;
    private readonly MethodInfo? _choices;

    internal @this(System.Type clr)
    {
        _clr = clr;
        _choices = clr.IsEnum ? null
            : clr.GetMethod("Choices", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
    }

    /// <summary>True when the CLR type carries options — an enum, or a static <c>Choices(context?)</c>.</summary>
    internal bool IsClosed => _clr.IsEnum || _choices != null;

    /// <summary>The set's plang name, as the set declares it. A closed set that declares none fails
    /// loud — its name is never derived from the CLR name.</summary>
    public string Name => _clr.GetCustomAttribute<global::app.Attributes.PlangTypeAttribute>(inherit: false)?.Name
        ?? throw new System.InvalidOperationException(
            $"closed set {_clr.FullName} declares no plang name — a closed set declares its name: [PlangType(\"…\")].");

    /// <summary>The options: an enum's member names, or the set's own <c>Choices(context?)</c>.</summary>
    public System.Collections.Generic.IReadOnlyList<string> Values
    {
        get
        {
            if (_clr.IsEnum) return System.Enum.GetNames(_clr);
            if (_choices == null) return System.Array.Empty<string>();
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
}
