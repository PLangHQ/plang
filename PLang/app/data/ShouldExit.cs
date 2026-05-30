using app.type;
using app.type.list;

namespace app.data;

/// <summary>
/// Unifies the three distinct stop conditions for the step loop into one
/// branch: unhandled failure, <see cref="@this.Returned"/>, or an Exit-typed
/// result. The flags stay distinct for downstream consumers — only the
/// step-loop short-circuit uses this aggregate.
///
/// Resolution order:
///   1. Unhandled failure or explicit Return → exit.
///   2. Value-side opt-out — an IExitsGoal instance can declare itself
///      "resolved" via <see cref="global::app.IExitsGoal.ShouldExit"/>, so a
///      typed-return like Data&lt;Ask&gt; with the answer bound flows through.
///   3. Type-side check — Data whose Value is null/non-IExitsGoal but whose
///      Type wraps an Exit-typed CLR type (path AuthGate's stateless suspend
///      forwards through this branch).
/// </summary>
public static class ShouldExitExtensions
{
    public static bool ShouldExit(this @this d)
    {
        if (!d.Success && !d.Handled) return true;
        if (d.Returned) return true;
        if (d.Value is global::app.IExitsGoal eg) return eg.ShouldExit();
        if (d.Type?.ClrType.Exit() == true) return true;
        return false;
    }
}
