namespace app.origin;

/// <summary>
/// A code several root apps are made from, so they read one scan of its facts: what each adds goes to its own layer
/// (its types, its modules, its readers), and the code takes only what gives every app the same answer. A kind on one of
/// the code's own types is the one thing that would reach every app: it is refused, as a sub app's is, so a root that
/// loads a DLL whose kinds land on a code type keeps a code of its own.
/// </summary>
internal sealed class shared : code
{
    /// <summary>A code for roots wired as <paramref name="wiring"/> says, each made from it reading its one scan.</summary>
    internal shared(Wiring wiring) : base(wiring) { }

    /// <summary>A kind on a type of this code would be every root's: refused.</summary>
    public override global::app.error.@this? Hold(global::app.type.kind.@this kind)
        => new($"'{kind.Name}' is a kind of {kind.Owner?.Name}, which this app shares with the other apps made from its code: "
            + $"an app can't add a kind to {kind.Owner?.Name} yet", "TypesShared", 400);
}
