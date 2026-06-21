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
        this global::app.variable.list.@this vars, string name)
    {
        // The door, not Materialize — a reference (file/url) yields its raw content.
        var v = await (await vars.Get(name)).Value();
        if (v is global::app.type.dict.@this or global::app.type.list.@this) return v;
        return v is global::app.type.item.@this iv ? iv.Clr<object>() : v;
    }
}
