namespace app.data;

/// <summary>
/// A scalar that can be reconciled with a text operand by parsing that text into its own
/// type. The one coercion mediator (<see cref="app.module.condition.Operator.NormalizeTypes"/>)
/// calls this on the typed side so <c>%date% == "2026-01-01"</c> (and ordering) coerces the
/// ISO string the same way <c>"5" == 5</c> coerces — without a per-type switch in the mediator.
/// Stateless: returns a fresh wrapper, or <c>null</c> when the text isn't a valid form of this
/// type (the mediator then leaves the pair unreconciled and the compare is false).
/// </summary>
public interface ITextCoercible
{
    object? CoerceText(string text);
}
