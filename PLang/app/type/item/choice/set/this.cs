using System.Reflection;

namespace app.type.item.choice.set;

/// <summary>
/// A closed set — the options a <c>choice</c> may take, and a kind of choice. The set is a CLR enum
/// (its members) or a class declaring a static <c>Choices(context?)</c>. Its plang name is its own
/// declaration (<c>[PlangType("operator")]</c>) and is the KIND of every choice drawn from it:
/// <c>{choice, kind: operator}</c>. Never a type of its own.
/// </summary>
public sealed class @this : global::app.type.kind.@this
{
    private readonly System.Type _clr;
    private readonly MethodInfo? _choices;

    /// <summary>The set <paramref name="clr"/> draws its options from. A closed set that declares no
    /// plang name fails loud — its name is never derived from the CLR name.</summary>
    internal @this(System.Type clr)
        : base(clr.GetCustomAttribute<global::app.Attributes.PlangTypeAttribute>(inherit: false)?.Name
               ?? (Closed(clr)
                   ? throw new System.InvalidOperationException(
                       $"closed set {clr.FullName} declares no plang name — a closed set declares its name: [PlangType(\"…\")].")
                   : clr.Name))
    {
        _clr = clr;
        _choices = Choices(clr);
    }

    /// <summary>A closed set is a kind of choice.</summary>
    protected internal override string Owner => "choice";

    /// <summary>True when the CLR type carries options — an enum, or a static <c>Choices(context?)</c>.</summary>
    internal bool IsClosed => Closed(_clr);

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

    private static bool Closed(System.Type clr) => clr.IsEnum || Choices(clr) != null;

    private static MethodInfo? Choices(System.Type clr)
        => clr.IsEnum ? null
            : clr.GetMethod("Choices", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
}
