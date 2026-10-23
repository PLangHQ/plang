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

    /// <summary>A closed set named <paramref name="name"/> over <paramref name="clr"/>, a kind of
    /// <paramref name="owner"/> — the app's choice.</summary>
    protected @this(System.Type clr, string name, global::app.type.@this owner) : base(name, owner)
        => _form = typeof(global::app.type.item.choice.@this<>).MakeGenericType(clr);

    /// <summary>The closed set <paramref name="clr"/> is, a kind of the choice <paramref name="types"/> holds — chosen
    /// once, here, by its shape; null when it is no closed set (no enum members, no <c>Choices(context?)</c>, no kind
    /// family).</summary>
    public static @this? For(System.Type clr, global::app.type.list.@this types)
    {
        var choice = types.Items().First(type => type.Names("choice"));
        return clr.IsEnum ? new member.@this(clr, choice)
             : family.@this.Is(clr) ? new family.@this(clr, choice, types)
             : named.@this.Choices(clr) is { } choices ? new named.@this(clr, choices, choice)
             : null;
    }

    /// <summary>A choice from this set is <c>choice&lt;T&gt;</c> closed over the set.</summary>
    public override System.Type? Of(System.Type? type) => _form;

    /// <summary>The options a choice from this set takes — the set's own answer to the type's values
    /// (a choice set IS a kind of its type; <c>%x!type.values%</c> reads this through <c>type.Values</c>).</summary>
    protected abstract override System.Collections.Generic.IReadOnlyList<string>? OwnValues { get; }

    /// <summary>A choice is one of its options, whatever the step's words — each a choice from this set, born as the
    /// T → choice operator births one (no app is asked for an offer).</summary>
    public override System.Threading.Tasks.ValueTask<System.Collections.Generic.IReadOnlyList<global::app.type.item.@this>> Offers(global::app.goal.step.@this step)
        => new(Values!.Select(option => (global::app.type.item.@this)System.Activator.CreateInstance(_form,
            BindingFlags.Instance | BindingFlags.NonPublic, null, [Member(option)], null)!).ToList());

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
