namespace app.type.choice;

/// <summary>
/// Non-generic view of a <see cref="@this{TEnum}"/> — lets the serializer and
/// Normalize recognize any choice without knowing the closed enum type.
/// </summary>
public interface IChoice
{
    /// <summary>The chosen option's name (the enum member name) — the bare wire form.</summary>
    string Name { get; }
    /// <summary>The boxed CLR enum value.</summary>
    System.Enum EnumValue { get; }
}

/// <summary>
/// PLang <c>choice</c> value — a value picked from a fixed set of named options
/// (the layperson's "enum"). Generic over the CLR enum so the handler keeps the
/// typed value (<c>HttpMethod m = action.Method?.Value ?? …</c> via the implicit
/// operator) while the language sees a validated choice. Aligns with the
/// <c>[Choices]</c> attribute — its options ARE the enum's names.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(JsonFactory))]
public sealed class @this<TEnum> : global::app.type.item.@this,
    global::app.data.IEquatableValue, IChoice
    where TEnum : struct, System.Enum
{
    public TEnum Value { get; }

    public @this(TEnum value) { Value = value; }

    public static implicit operator TEnum(@this<TEnum> c) => c.Value;

    public override object? ToRaw() => Value;
    public override string ToString() => Value.ToString();
    public override bool IsTruthy() => true;
    public override bool IsLeaf => true;
    public override void Write(global::app.channel.serializer.IWriter w) => w.String(Value.ToString());

    string IChoice.Name => Value.ToString();
    System.Enum IChoice.EnumValue => Value;

    /// <summary>The set of valid option names — the validation surface.</summary>
    public static System.Collections.Generic.IReadOnlyList<string> ValidValues
        => System.Enum.GetNames(typeof(TEnum));

    // Equality by enum value, and by name against a text/string (so `where method
    // equals 'GET'` reconciles).
    public bool AreEqual(object? other) => other switch
    {
        @this<TEnum> c => Value.Equals(c.Value),
        TEnum e => Value.Equals(e),
        IChoice ic => ic.EnumValue.Equals((System.Enum)Value),
        global::app.type.text.@this t => string.Equals(Value.ToString(), t.Value, System.StringComparison.OrdinalIgnoreCase),
        string s => string.Equals(Value.ToString(), s, System.StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    public override bool Equals(object? obj) => AreEqual(obj);
    public override int GetHashCode() => Value.GetHashCode();
}
