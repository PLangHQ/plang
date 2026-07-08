using System.Reflection;

namespace app.type.kind.behavior.list;

/// <summary>
/// The kind behaviors — every <see cref="app.type.kind.behavior.@this"/> (json, <c>*</c>,
/// dict, later yaml/xml), keyed by its <see cref="app.type.kind.behavior.@this.Kind"/>
/// token (value equality by name). The store the kind token delegates into; an unknown
/// kind falls back to the <c>*</c> reflection behavior — the catch-all — so a clr whose
/// object names no registered format still navigates. Reached only through the kind token,
/// never a flat <c>App.Type.&lt;plural&gt;</c>.
///
/// <para>Also owns the CLR-bridge reverse map: given a host object's CLR type, which kind
/// is it (json → <see cref="System.Text.Json.JsonElement"/>). The clr's kind resolution
/// asks here at birth, never a loose ResolveName.</para>
///
/// <para>Behaviors are app-independent pure logic, so the App-assembly scan runs ONCE per
/// process (static, born at type load — no per-App reflection). A per-App runtime-DLL seam
/// (for a future <c>- add type &lt;dll&gt;</c> action) is added WITH that action, not
/// speculatively.</para>
/// </summary>
public sealed class @this
{
    private static readonly global::app.type.kind.@this Any = "*";   // the catch-all reflection kind
    private static readonly Discovered _shared = new(typeof(@this).Assembly);

    /// <summary>The behavior for <paramref name="kind"/>, or the <c>*</c> reflection
    /// catch-all when the kind names no registered format.</summary>
    public global::app.type.kind.behavior.@this this[global::app.type.kind.@this kind]
        => _shared.Behavior(kind) ?? _shared.Behavior(Any)!;

    /// <summary>The kind a host object of <paramref name="clrType"/> is when a behavior
    /// CLAIMS that CLR form (a <see cref="System.Text.Json.JsonElement"/> → <c>json</c>),
    /// else null — the clr then falls back to the host's plang-type identity. Not <c>*</c>:
    /// an unclaimed host still has a class identity (a callstack is kind "callstack").</summary>
    public global::app.type.kind.@this? this[System.Type clrType]
        => _shared.Kind(clrType);

    // A scanned set of behaviors + their CLR-form reverse map. Immutable once built.
    private sealed class Discovered
    {
        private readonly System.Collections.Generic.Dictionary<global::app.type.kind.@this, global::app.type.kind.behavior.@this> _byKind = new();
        private readonly System.Collections.Generic.Dictionary<System.Type, global::app.type.kind.@this> _byClr = new();

        public Discovered(Assembly assembly)
        {
            foreach (var t in assembly.GetTypes())
                if (typeof(global::app.type.kind.behavior.@this).IsAssignableFrom(t) && t is { IsAbstract: false })
                {
                    var b = (global::app.type.kind.behavior.@this)System.Activator.CreateInstance(t)!;
                    _byKind[b.Kind] = b;
                    if (b.ClrForm is { } clr) _byClr[clr] = b.Kind;
                }
        }

        public global::app.type.kind.behavior.@this? Behavior(global::app.type.kind.@this kind)
            => _byKind.TryGetValue(kind, out var b) ? b : null;

        public global::app.type.kind.@this? Kind(System.Type clrType)
            => _byClr.TryGetValue(clrType, out var k) ? k : null;
    }
}
