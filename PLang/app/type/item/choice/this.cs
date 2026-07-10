using System.Reflection;

namespace app.type.item.choice;

/// <summary>
/// Non-generic view of a <see cref="@this{T}"/> — lets the serializer and Normalize
/// recognize any choice without knowing the closed option type. The bare wire form
/// is the chosen option's name.
/// </summary>
public interface IChoice
{
    /// <summary>The chosen option's name — the bare wire form.</summary>
    string Name { get; }
}

/// <summary>
/// PLang <c>choice</c> value — a value picked from a fixed set of named options
/// (the layperson's "enum"). Generic over ANY named-set type, not just CLR enums:
/// <list type="bullet">
///   <item>a CLR <c>enum</c> — options are its member names (GetNames / Parse);</item>
///   <item>a class that declares a closed set — a static <c>Choices(context?)</c>
///   method (the <c>[Choices]</c> vocabulary) plus a <c>ctor(string)</c> that
///   resolves a name (e.g. <c>condition.Operator</c>, which keeps its per-option
///   behavior on the instance).</item>
/// </list>
/// The handler keeps the typed value (<c>HttpMethod m = action.Method?.Clr<string>()</c> via
/// the implicit operator) while the language sees a validated choice. The name is the
/// value's <see cref="object.ToString"/> — uniform across enums and named-set classes.
/// </summary>
public sealed class @this<T> : global::app.type.item.@this, global::app.type.item.ICreate<@this<T>>,
    IChoice
    where T : notnull
{
    // A choice knows how to resolve its OWN option set — computed once per closed type
    // (static members on a generic are per-T). An enum reads its members directly; a
    // named-set class exposes a static Choices(context?) + a ctor(string).
    private static readonly MethodInfo? _choicesMethod = typeof(T).IsEnum ? null
        : typeof(T).GetMethod("Choices", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
    private static readonly ConstructorInfo? _nameCtor = typeof(T).IsEnum ? null
        : typeof(T).GetConstructor(new[] { typeof(string) });

    public T Value { get; }

    public @this(T value) { Value = value; }

    public static implicit operator T(@this<T> c) => c.Value;
    // T → choice (so `.Ok(HttpMethod.GET)` and a `[Default(HttpMethod.GET)]` cast construct).
    public static implicit operator @this<T>(T value) => new(value);

    /// <summary>The CLR exit door — choice hands its enum backing.</summary>
    internal override object? Clr(System.Type target) => ClrConvert(Value, target);
    public override string ToString() => Value.ToString() ?? "";
    public override bool IsTruthy() => true;
    public override bool IsLeaf => true;
    public override void Write(global::app.channel.serializer.IWriter w) => w.String(ToString());

    string IChoice.Name => ToString();

    /// <summary>The set of valid option names — the validation surface (LLM [Choices]).
    /// The choice reads its OWN options: an enum's member names, or a static Choices(context?).</summary>
    public static System.Collections.Generic.IReadOnlyList<string> ValidValues => Names(null);

    private static System.Collections.Generic.IReadOnlyList<string> Names(global::app.actor.context.@this? context)
    {
        if (typeof(T).IsEnum) return System.Enum.GetNames(typeof(T));
        if (_choicesMethod != null)
        {
            object?[] args = _choicesMethod.GetParameters().Length == 1
                ? new object?[] { context } : System.Array.Empty<object?>();
            return _choicesMethod.Invoke(null, args) switch
            {
                string[] arr => arr,
                System.Collections.Generic.IReadOnlyList<string> list => list,
                System.Collections.Generic.IEnumerable<string> seq => new System.Collections.Generic.List<string>(seq),
                _ => System.Array.Empty<string>(),
            };
        }
        throw new System.InvalidOperationException(
            $"choice<{typeof(T).Name}>: not a named-set type — needs an enum, or a static Choices(context?) method.");
    }

    /// <summary>Resolve a chosen name to a typed choice — the choice builds ITSELF from its
    /// option name (an enum member, or the named-set class's ctor(string)). Throws if invalid.</summary>
    public static @this<T> FromName(string name, global::app.actor.context.@this? context)
    {
        object member = typeof(T).IsEnum
            ? System.Enum.Parse(typeof(T), name, ignoreCase: true)
            : _nameCtor?.Invoke(new object?[] { name })
              ?? throw new System.InvalidOperationException(
                  $"choice<{typeof(T).Name}>: cannot resolve '{name}' — needs an enum or a ctor(string).");
        return new((T)member);
    }

    /// <summary>OBP: <c>choice&lt;T&gt;</c> builds itself from a chosen option NAME (a
    /// string, or any value's ToString). The CONVERT hook the catalog discovers on the
    /// closed type — replaces the special choice arm in TryConvert.</summary>
    public static global::app.data.@this Convert(object? value, string? kind,
        global::app.actor.context.@this context)
    {
        var chosen = value as string ?? value?.ToString() ?? "";
        try { return context.Ok(FromName(chosen, context)); }
        catch (System.Exception ex)
        {
            var inner = (ex as System.Reflection.TargetInvocationException)?.InnerException ?? ex;
            return context.Error(new global::app.error.Error(
                $"'{chosen}' is not a valid {typeof(T).Name} option: {inner.Message}",
                "TypeConversionFailed", 400));
        }
    }

    // ---- Comparison (the unified hook — see app.type.compare; statics on the
    // generic exist per closed type, so discovery via the catalog's closed
    // choice<T> finds them) ----

    /// <summary>Outranks text — the name string coerces into the choice.</summary>
    public override int Rank => 150;

    /// <summary>Equality-only: <c>Equal</c>/<c>NotEqual</c> by value or by name
    /// (choice/text), never an order. This choice drives; the other coerces via
    /// <see cref="AreEqual"/> (a name string / choice matches its member).</summary>
    protected override System.Threading.Tasks.ValueTask<global::app.data.Comparison> Order(global::app.type.item.@this other)
        => new(AreEqual(other) ? global::app.data.Comparison.Equal : global::app.data.Comparison.NotEqual);

    // Equality by value, and by name against a choice/text/string (so
    // `where method equals 'GET'` reconciles).
    public bool AreEqual(object? other) => other switch
    {
        @this<T> c => Value.Equals(c.Value),
        T v => Value.Equals(v),
        IChoice ic => string.Equals(ToString(), ic.Name, System.StringComparison.OrdinalIgnoreCase),
        global::app.type.item.text.@this t => t.AreEqual(ToString()),
        string s => string.Equals(ToString(), s, System.StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    public override bool Equals(object? obj) => AreEqual(obj);
    public override int GetHashCode() => Value.GetHashCode();
}
