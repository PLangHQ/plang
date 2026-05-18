namespace app.variables;

/// <summary>
/// Marker for types whose <c>static Resolve(string, Context.@this)</c> wants the
/// <em>raw</em> slot string — Data.As&lt;T&gt; must skip its <c>%var%</c> substitution
/// branch and dispatch to <c>Resolve</c> directly.
///
/// <para>
/// Default flow for a slot whose value is <c>"%x%"</c>: <c>Data.AsT_Impl</c> intercepts
/// at the substitution branch (TryFullVarMatch → Variables.Get), expecting the
/// referenced variable's <em>value</em> in T form. That's correct for value-typed Ts
/// (string, int, FileSystem.path) where <c>%x%</c> means "interpolate the value of x".
/// </para>
/// <para>
/// For name-like Ts (Variable), <c>%x%</c> means "the variable named x" — the slot is
/// asking for the <em>identity</em>, not the value. The marker tells <c>AsT_Impl</c> to
/// hand the raw <c>"%x%"</c> straight to <see cref="app.variables.Variable.Resolve"/>,
/// which strips the <c>%</c> and produces <c>Variable { Name = "x" }</c> regardless of
/// whether x is initialized.
/// </para>
/// <para>
/// Empty marker — no methods. Implementers carry the rule by their type alone.
/// </para>
/// </summary>
public interface IRawNameResolvable { }
