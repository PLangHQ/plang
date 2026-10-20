namespace PLang.Tests;

/// <summary>
/// The test session — one per test process. Each test still gets a fresh app of its own; every app is born from
/// plang's code read once for the session (<see cref="global::app.assembly.@this"/>), not re-read at each birth.
/// </summary>
public static class Session
{
    /// <summary>Plang's own code, read once for the session.</summary>
    public static readonly global::app.assembly.@this Code = new(typeof(global::app.@this).Assembly);

    /// <summary>A fresh app, as <c>new app(…)</c> makes one, born from the session's code.</summary>
    public static global::app.@this App(string absolutePath, string? environment = null, bool autoWireConsoleChannels = true)
        => new(absolutePath, environment, autoWireConsoleChannels, Code);
}
