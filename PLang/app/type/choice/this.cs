using System.Collections.Concurrent;
using System.Reflection;

namespace app.type.choice;

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
[System.Text.Json.Serialization.JsonConverter(typeof(JsonFactory))]
public sealed class @this<T> : global::app.type.item.@this, global::app.type.item.ICreate<@this<T>>,
    IChoice
    where T : notnull
{
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

    /// <summary>The set of valid option names — the validation surface (LLM [Choices]).</summary>
    public static System.Collections.Generic.IReadOnlyList<string> ValidValues
        => ChoiceMeta.Names(typeof(T), null);

    /// <summary>Resolve a chosen name to a typed choice. Throws if the name isn't a valid option.</summary>
    public static @this<T> FromName(string name, global::app.actor.context.@this? context)
        => new((T)ChoiceMeta.FromName(typeof(T), name, context));

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
    internal static int CompareRank => 15;

    /// <summary>Equality-only: <c>Equal</c>/<c>NotEqual</c> by value or by name
    /// (choice/text/string), never an order. Neither side a choice → Incomparable.</summary>
    public static global::app.data.Comparison Compare(object? a, object? b)
    {
        if (a is @this<T> ca)
            return ca.AreEqual(b) ? global::app.data.Comparison.Equal : global::app.data.Comparison.NotEqual;
        if (b is @this<T> cb)
            return cb.AreEqual(a) ? global::app.data.Comparison.Equal : global::app.data.Comparison.NotEqual;
        return global::app.data.Comparison.Incomparable;
    }

    // Equality by value, and by name against a choice/text/string (so
    // `where method equals 'GET'` reconciles).
    public bool AreEqual(object? other) => other switch
    {
        @this<T> c => Value.Equals(c.Value),
        T v => Value.Equals(v),
        IChoice ic => string.Equals(ToString(), ic.Name, System.StringComparison.OrdinalIgnoreCase),
        global::app.type.text.@this t => t.AreEqual(ToString()),
        string s => string.Equals(ToString(), s, System.StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    public override bool Equals(object? obj) => AreEqual(obj);
    public override int GetHashCode() => Value.GetHashCode();
}

/// <summary>
/// Reflection adapter that gives any named-set type T its option names and a
/// name→value factory. Enums use <see cref="System.Enum"/>; a named-set class uses
/// its static <c>Choices(context?)</c> method and a <c>ctor(string)</c>. Cached per T.
/// </summary>
internal static class ChoiceMeta
{
    private static readonly ConcurrentDictionary<System.Type, MethodInfo?> _choicesMethod = new();
    private static readonly ConcurrentDictionary<System.Type, ConstructorInfo?> _nameCtor = new();

    public static System.Collections.Generic.IReadOnlyList<string> Names(System.Type t, global::app.actor.context.@this? context)
    {
        if (t.IsEnum) return System.Enum.GetNames(t);
        MethodInfo? m = _choicesMethod.GetOrAdd(t, static ty =>
            ty.GetMethod("Choices", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy));
        if (m != null)
        {
            object?[] args = m.GetParameters().Length == 1 ? new object?[] { context } : System.Array.Empty<object?>();
            object? result = m.Invoke(null, args);
            return result switch
            {
                string[] arr => arr,
                System.Collections.Generic.IReadOnlyList<string> list => list,
                System.Collections.Generic.IEnumerable<string> seq => new System.Collections.Generic.List<string>(seq),
                _ => System.Array.Empty<string>(),
            };
        }
        throw new System.InvalidOperationException(
            $"choice<{t.Name}>: not a named-set type — needs an enum, or a static Choices(context?) method.");
    }

    public static object FromName(System.Type t, string name, global::app.actor.context.@this? context)
    {
        if (t.IsEnum) return System.Enum.Parse(t, name, ignoreCase: true);
        ConstructorInfo? ctor = _nameCtor.GetOrAdd(t, static ty => ty.GetConstructor(new[] { typeof(string) }));
        if (ctor != null) return ctor.Invoke(new object?[] { name });
        throw new System.InvalidOperationException(
            $"choice<{t.Name}>: cannot resolve '{name}' — needs an enum or a ctor(string).");
    }
}
