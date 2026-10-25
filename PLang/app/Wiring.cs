namespace app;

/// <summary>What an app made from the code is born wired to: the console's standard channels (<see cref="console"/>,
/// its System and User wired when each first uses its channels, their input a non-blocking EOF sink), or none
/// (<see cref="none"/>: the caller wires them — the CLI binds real stdin).</summary>
public enum Wiring
{
    console,
    none,
}
