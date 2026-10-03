using System.Reflection;

namespace app.type.item.choice.set;

/// <summary>
/// A closed set — the options a <c>choice</c> may take, and a kind of choice: an enum's members
/// (<see cref="member.@this"/>), a class naming its own (<see cref="named.@this"/>: a static <c>Choices(context?)</c>
/// and a <c>ctor(string)</c>), or a kind family's kinds (<see cref="family.@this"/>: <c>hash.kind</c> → sha256,
/// keccak256). Each answers its own options and the member a symbol names. Its plang name is the KIND of every choice
/// drawn from it: <c>{choice, kind: operator}</c>. Never a type of its own.
/// </summary>
public abstract class @this : global::app.type.kind.@this
{
    private readonly System.Type _form;

    protected @this(System.Type clr, string name) : base(name)
        => _form = typeof(global::app.type.item.choice.@this<>).MakeGenericType(clr);

    /// <summary>The closed set <paramref name="clr"/> is — chosen once, here, by its shape; null when it is no closed
    /// set (no enum members, no <c>Choices(context?)</c>, no kinds of its own).</summary>
    public static @this? For(System.Type clr)
        => clr.IsEnum ? new member.@this(clr)
         : family.@this.Kinds(clr) is { Count: > 0 } kinds ? new family.@this(clr, kinds)
         : named.@this.Choices(clr) is { } choices ? new named.@this(clr, choices)
         : null;

    /// <summary>A closed set is a kind of choice.</summary>
    protected internal override string Owner => "choice";

    /// <summary>A choice from this set is <c>choice&lt;T&gt;</c> closed over the set.</summary>
    public override System.Type? Of(System.Type? type) => _form;

    /// <summary>The options a choice from this set takes.</summary>
    public abstract override System.Collections.Generic.IReadOnlyList<string> Values { get; }

    /// <summary>A choice is one of its options, whatever the step's words — each a choice from this set.</summary>
    public override System.Threading.Tasks.ValueTask<System.Collections.Generic.IReadOnlyList<global::app.type.item.@this>> Offers(global::app.goal.step.@this step)
        => new(Values.Select(option => (global::app.type.item.@this)System.Activator.CreateInstance(_form, Member(option))!).ToList());

    /// <summary>A choice is none but its options.</summary>
    public override bool IsClosed => true;

    /// <summary>The member <paramref name="symbol"/> names; throws <see cref="System.ArgumentException"/> for a name
    /// that is none of the options.</summary>
    public abstract object Member(string symbol);

    /// <summary>The name a set declares (<c>[PlangType("operator")]</c>) — never derived from its CLR name.</summary>
    protected static string Declared(System.Type clr)
        => clr.GetCustomAttribute<global::app.Attributes.PlangTypeAttribute>(inherit: false)?.Name
           ?? throw new System.InvalidOperationException(
               $"closed set {clr.FullName} declares no plang name — a closed set declares its name: [PlangType(\"…\")].");
}
