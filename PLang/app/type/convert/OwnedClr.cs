namespace app.type.convert;

/// <summary>
/// One CLR type a type family claims ownership of — the distributed replacement
/// for the central <c>OwnerOf</c> switch. A family declares these on a static
/// <c>OwnedClrTypes</c> property (e.g. <c>number</c> declares int/long/decimal/…);
/// <see cref="@this.OwnerOf"/> composes the <c>clr → (family, kind)</c> routing
/// from every family's declarations, so adding a CLR type a family owns is an
/// edit to that family alone — never to <c>convert</c>.
/// </summary>
/// <param name="Clr">The CLR type owned.</param>
/// <param name="Kind">The kind to stamp when this CLR type pins one (number precision); null otherwise.</param>
/// <param name="Assignable">
/// When true, the family owns <see cref="Clr"/> <em>and every subclass of it</em>
/// (path owns all its scheme subclasses). When false, only the exact type matches.
/// </param>
public sealed record OwnedClr(System.Type Clr, string? Kind = null, bool Assignable = false);
