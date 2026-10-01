namespace app.type.item;

/// <summary>
/// A type whose value has a default of its own — an action option record (<c>limit</c>, <c>redirect</c>) whose
/// members each carry theirs. A slot of such a type that the step leaves out holds <see cref="Default"/>, as a
/// <c>[Default]</c> literal would: the catalog shows it, a build freezes it, its setting reads it.
/// </summary>
public interface IDefault<TSelf> where TSelf : @this, IDefault<TSelf>
{
    /// <summary>The value a slot of this type holds when nothing sets it.</summary>
    static abstract TSelf Default { get; }
}
