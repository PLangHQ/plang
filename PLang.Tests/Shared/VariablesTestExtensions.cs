namespace PLang.Tests.Shared;

/// <summary>
/// Test-only convenience for reading a variable's raw CLR value by name. Production
/// reads values through the real doors — <c>(await Variable.Get(name)).Clr&lt;T&gt;(fallback)</c>
/// for a typed scalar, <c>.Value()</c> / <c>.Peek()</c> for the wrapper. This shim keeps
/// the terse "give me the backing object" form that assertions want, and lives in the
/// test assembly rather than in runtime.
///
/// A scalar rides as its wrapper (text/number/bool/…); this unwraps it to the backing
/// CLR (text→string, number→numeric, datetime→DateTimeOffset). Collections stay native
/// (dict/list keep their plang type).
/// </summary>
public static class VariablesTestExtensions
{
    public static async System.Threading.Tasks.ValueTask<object?> GetValue(
        this global::app.type.item.variable.list.@this vars, string name)
    {
        // The door, not Materialize — a reference (file/url) yields its raw content. A name may be a
        // path (`Now.Ticks`): its root is read from this store, the rest walked from there.
        var root = new global::app.type.item.variable.@this(name).Code.Root.Name;
        var held = await vars.Get(root);
        if (name.Length > root.Length) held = await held.Get(name[root.Length..]);
        var v = await held.Value();
        if (v is global::app.type.item.dict.@this or global::app.type.item.list.@this) return v;
        return v is global::app.type.item.@this iv ? iv.Clr<object>() : v;
    }
}
