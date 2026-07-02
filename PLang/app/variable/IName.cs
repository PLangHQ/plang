namespace app.variable;

/// <summary>
/// A value of this type IS a name — it refers to a variable/slot by name, not by
/// carrying a rendered value. Only <see cref="@this"/> is an <c>IName</c>.
///
/// <para>
/// Two consumers rely on it. (1) <c>Data.As&lt;T&gt;</c> hands a raw <c>"%x%"</c>
/// straight to <see cref="@this.Resolve"/> (skipping <c>%var%</c> substitution), so
/// <c>%x%</c> yields <c>variable{ Name = "x" }</c> — the identity, not the value.
/// (2) the source generator detects a <c>Data&lt;IName&gt;</c> slot at compile time (it
/// cannot <c>typeof</c> the runtime type from a netstandard2.0 analyzer) and emits the
/// required-parameter guard for a non-nullable name slot.
/// </para>
/// <para>
/// Empty marker — no methods. Runtime code that has the loaded type compares against
/// <c>typeof(app.variable.@this)</c> directly; the interface exists for the generator.
/// </para>
/// </summary>
public interface IName { }
