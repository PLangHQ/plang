namespace app.type.item.variable;

/// <summary>
/// A value of this type IS a name — it refers to a variable/slot by name, not by
/// carrying a rendered value. Only <see cref="@this"/> is an <c>IName</c>.
///
/// <para>
/// The one declaration of the fact. Runtime code asks it through the type's static
/// channel, <see cref="global::app.type.item.ICreate{TSelf}.IsName"/> (derived from this
/// marker): the type's build makes a name type's text into the name at once instead of
/// deferring it as content, and the typed ask hands a held name over without opening it.
/// The source generator reads it at compile time (it cannot <c>typeof</c> the runtime type
/// from a netstandard2.0 analyzer) and emits the required-parameter guard for a
/// non-nullable name slot.
/// </para>
/// </summary>
public interface IName { }
