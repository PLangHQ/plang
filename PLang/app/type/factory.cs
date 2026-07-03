namespace app.type;

/// <summary>
/// Context-bound minter for type-entities — reached as <c>context.Type</c>. A type minted
/// here is born WITH the calling context (the context you're in is the context the type gets),
/// so its App-keyed schema fold resolves without a later stamp. Replaces the static
/// <c>type.@this.FromName</c>: minting is an operation of a scope, not a free function.
/// </summary>
public readonly struct factory
{
    private readonly actor.context.@this _context;
    public factory(actor.context.@this context) => _context = context;

    /// <summary>Borns a type-entity by name (and optional kind/strict/template) stamped with
    /// this context — the canonicalising path (<see cref="@this.Create(string, string?, bool,
    /// actor.context.@this?, string?)"/>).</summary>
    public @this Create(string name, string? kind = null, bool strict = false, string? template = null)
        => @this.Create(name, kind, strict, _context, template);
}
