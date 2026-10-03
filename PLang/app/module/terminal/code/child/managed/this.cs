namespace app.module.terminal.code.child.managed;

/// <summary>A program .NET started (<see cref="System.Diagnostics.Process"/>): every program, on every system.</summary>
internal sealed class @this(System.Diagnostics.Process process) : child.@this
{
    /// <summary>The .NET process, for what only it does: Windows' job (it ends with plang), running a program to its end.</summary>
    internal System.Diagnostics.Process Process => process;

    internal override int Id => process.Id;
    internal override bool HasExited => process.HasExited;
    internal override int ExitCode => process.ExitCode;
    internal override StreamWriter Input => process.StandardInput;
    internal override StreamReader Output => process.StandardOutput;
    internal override StreamReader Error => process.StandardError;

    internal override Task WaitForExitAsync(CancellationToken ct = default) => process.WaitForExitAsync(ct);

    internal override void Kill() => process.Kill(entireProcessTree: true);

    public override void Dispose() => process.Dispose();
}
