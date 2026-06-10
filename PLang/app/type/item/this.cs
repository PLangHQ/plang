namespace app.type.item;

/// <summary>
/// The apex of the PLang value lattice (≈ C# <c>object</c>) <em>and</em> the
/// un-narrowed type tag a value carries before it is examined. Every value
/// wrapper — <c>number</c>, <c>text</c>, <c>dict</c>, <c>list</c>, <c>bool</c>,
/// <c>null</c>, the date-family, <c>duration</c>, <c>path</c>/<c>image</c>/<c>code</c>,
/// <c>Variable</c>, <c>Ask</c> — is a <c>: item.@this</c>, so a <c>Data&lt;T&gt;</c>
/// slot can constrain <c>where T : item</c> and a raw CLR value can never ride a
/// value slot.
///
/// <para><b>Storage-free.</b> Stamping a type does not parse: an un-narrowed
/// value's serialized form rides on <c>Data</c> (lazy materialization,
/// <c>type-system.md</c>), not on this base. So <c>item</c> owns no value slot —
/// the backing (<c>int</c>/<c>string</c>/<c>List&lt;Data&gt;</c>) stays on each
/// subtype. <c>item</c> carries only <em>behavior</em>, never a blob field, so a
/// narrowed subtype (<c>number</c>/<c>dict</c>/<c>Ask</c>) inherits no dead
/// un-narrowed state (OBP smell #6).</para>
///
/// <para><b>The universal contract is truthiness + the lazy narrow only.</b>
/// Ordering (<see cref="global::app.data.IOrderableValue"/>) and value-equality
/// (<see cref="global::app.data.IEquatableValue"/>) stay opt-in interfaces — a
/// value implements them only when it honors them (<c>list</c> orders, <c>dict</c>
/// does not). <c>item</c> must <b>not</b> implement either, or <c>dict : item</c>
/// would inherit an order it can't honor (its <c>Compare.Order</c> throws).</para>
/// </summary>
public abstract class @this : global::app.data.IBooleanResolvable
{
    /// <summary>
    /// Synchronous truthiness — the hot path so a plain <c>if %bool%</c> never
    /// takes an async hop. The default is "reference-ish item is truthy when
    /// present"; concrete types override (empty text / zero / empty collection /
    /// null are falsy). I/O truthiness (<c>path</c> existence) overrides
    /// <see cref="AsBooleanAsync"/> instead.
    /// </summary>
    public virtual bool IsTruthy() => true;

    /// <summary>
    /// THE CLR EXIT DOOR — converts this value to the raw CLR
    /// <paramref name="target"/>. The pattern is uniform across types: the
    /// object hands ITS OWN backing to the shared mechanical converter
    /// (<see cref="ClrConvert"/>) in a one-line override; the type's own loss
    /// policy applies before the hand-over (number's tower). Lossy conversion
    /// THROWS — never a silent default. The base THROWS too: a type that
    /// declared no backing surfaces loudly the first time someone needs its
    /// CLR form, instead of guessing through a string. Internal — engine
    /// plumbing for the typed boundary (<c>Data.Clr&lt;T&gt;</c>, hydration);
    /// the plang-facing surface stays fully typed.
    /// </summary>
    internal virtual object? Clr(System.Type target)
        => throw new System.NotSupportedException(
            $"'{GetType().FullName}' declares no CLR backing — override Clr(Type) with the type's own backing to convert to {target.Name}.");

    /// <summary>Generic sugar over <see cref="Clr(System.Type)"/> — the
    /// compile-time-known-target form.</summary>
    internal T? Clr<T>() => (T?)Clr(typeof(T));

    /// <summary>
    /// The shared mechanics under every <see cref="Clr(System.Type)"/>: the
    /// value the TYPE handed over (its own backing — ownership stays with the
    /// type) converted via the engine's one converter. Identity short-circuits;
    /// failure throws loudly with the converter's bind message.
    /// </summary>
    private protected static object? ClrConvert(object? backing, System.Type target)
    {
        if (backing == null) return null;
        if (target.IsInstanceOfType(backing)) return backing;

        // Primitive targets convert directly (invariant, throws on junk/overflow)
        // — BEFORE the engine converter, whose family-hook arm would answer a
        // numeric target with the born-native WRAPPER (its job is the opposite
        // direction: CLR → plang).
        if (backing is System.IConvertible && typeof(System.IConvertible).IsAssignableFrom(target)
            && target != typeof(object) && !target.IsEnum)
            return System.Convert.ChangeType(backing, target, System.Globalization.CultureInfo.InvariantCulture);

        var (converted, error) = global::app.type.catalog.@this.TryConvert(backing, target);
        if (error != null)
            throw new System.InvalidCastException(error.Message);
        // The converter answered with a born-native wrapper (its family-hook
        // arm) — ask the WRAPPER for the raw target; its backing terminates the
        // recursion at the identity arm.
        if (converted is @this wrapper && !target.IsInstanceOfType(converted))
            return wrapper.Clr(target);
        return converted;
    }

    /// <summary>
    /// <see cref="global::app.data.IBooleanResolvable"/> — defaults to the sync
    /// <see cref="IsTruthy"/>. Only a value whose truthiness needs I/O
    /// (<c>path</c>) overrides this.
    /// </summary>
    public virtual System.Threading.Tasks.Task<bool> AsBooleanAsync()
        => System.Threading.Tasks.Task.FromResult(IsTruthy());

    /// <summary>
    /// The lazy narrow: an un-narrowed value (<c>type=item, kind=json</c>) reads
    /// its raw form at first touch and re-stamps itself <c>dict</c>/<c>list</c>.
    /// The default — for an already-narrowed subtype — is a no-op that returns
    /// <c>self</c>, so a typed value carries no un-narrowed state.
    /// </summary>
    public virtual @this Narrow() => this;

    /// <summary>
    /// True for a scalar leaf — a value with no sub-structure that rides the wire as
    /// a single bare token (text/number/bool/date-family/duration/binary/null/choice).
    /// Normalize passes a leaf straight to the writer (which renders it bare); a
    /// non-leaf (<c>dict</c>/<c>list</c> with children, <c>path</c>/<c>image</c>/<c>code</c>
    /// and other domain values that reflect) takes its own normalization branch. A new
    /// scalar type opts in here instead of being listed inside Normalize.
    /// </summary>
    public virtual bool IsLeaf => false;

    /// <summary>
    /// Render this value's bare wire form into the format-neutral
    /// <see cref="global::app.channel.serializer.IWriter"/> — the leaf-serializer
    /// behavior (OBP Rule 9: the value owns its wire shape, the writer never
    /// type-switches). Only leaves are asked (Normalize routes non-leaves through
    /// their own branches); the default throws so a missing override is loud.
    /// </summary>
    public virtual void Write(global::app.channel.serializer.IWriter writer)
        => throw new System.NotSupportedException(
            $"{GetType().Name} has no bare wire form — it is not a leaf value.");

    /// <summary>
    /// The raw CLR projection of this value — the single unwrap at the
    /// typed-conversion leaf (<c>→ returns string/int/DateTime/…</c>). A scalar
    /// returns its backing CLR scalar (<c>text → string</c>, <c>number → the
    /// boxed numeric</c>, <c>bool → bool</c>, the date-family → their CLR struct);
    /// <c>dict</c>/<c>list</c> decompose to a raw <c>Dictionary</c>/<c>List</c>;
    /// <c>null</c> → C# <c>null</c>. The default — a domain value that <em>is</em>
    /// its own raw form (<c>path</c>/<c>image</c>/<c>code</c>) — returns
    /// <c>self</c>. This replaces the per-type unwrap switch: the value owns its
    /// own raw projection, so the conversion leaf asks once with no type-switch.
    /// </summary>
    public virtual object? ToRaw() => this;
}
