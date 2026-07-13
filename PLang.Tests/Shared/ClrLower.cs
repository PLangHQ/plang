namespace PLang.Tests.Shared;

/// <summary>
/// Test-only convenience: lower a value-door answer to its CLR shape. Production removed the
/// <c>item.@this.Lower&lt;T&gt;</c> static — call sites now lower through the value's OWN
/// <c>.Clr&lt;T&gt;()</c>. Tests keep this shorthand (exposed unqualified via a global using static)
/// for assertions over a door answer that may be an item OR already-raw CLR.
/// </summary>
public static class ClrLower
{
    public static T? Lower<T>(object? doorAnswer) => doorAnswer switch
    {
        global::app.type.item.@this it => it.Clr<T>(),
        T t => t,
        _ => default,
    };
}
