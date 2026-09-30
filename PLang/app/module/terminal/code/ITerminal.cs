using app.module.code;

namespace app.module.terminal.code;

/// <summary>
/// Terminal provider. The action passes itself — the provider owns finding, permitting and running
/// the program. Swappable via app.Code (a test can run programs without real processes).
/// </summary>
public interface ITerminal : ICode
{
    /// <summary>Runs the program to its end; the value is its stdout as text.</summary>
    Task<data.@this<global::app.type.item.text.@this>> Start(start action);

    /// <summary>Starts the program and returns at once; it keeps running.</summary>
    Task<data.@this<Process>> Open(open action);

    /// <summary>Writes a line to a running program's stdin.</summary>
    Task<data.@this> Send(send action);

    /// <summary>Waits for a running program to exit; the value is its exit code.</summary>
    Task<data.@this<global::app.type.item.number.@this>> Wait(wait action);

    /// <summary>Ends a running program.</summary>
    Task<data.@this> Stop(stop action);
}
