using App.Types;

namespace App.Data;

/// <summary>
/// Unifies the three distinct stop conditions for the step loop into one
/// branch: unhandled failure, <see cref="@this.Returned"/>, or an Exit-typed
/// result. The flags stay distinct for downstream consumers — only the
/// step-loop short-circuit uses this aggregate.
/// </summary>
public static class ShouldExitExtensions
{
    public static bool ShouldExit(this @this d) =>
        (!d.Success && !d.Handled)
        || d.Returned
        || d.Type?.ClrType.Exit() == true;
}
