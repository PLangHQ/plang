namespace app.goal.kind.key;

/// <summary>
/// A goal written as what finds it, <c>{name, pr, hash}</c>: a value found elsewhere (the program) is saved as its key
/// and found again the first time it is used. Read back as that record (<c>goal/serializer/Key.cs</c>); the goal's
/// <c>Select</c> finds the live goal from it.
/// </summary>
public sealed class @this : global::app.type.kind.@this<global::app.goal.@this>
{
    public @this(global::app.type.@this owner) : base("key", owner) { }
}
