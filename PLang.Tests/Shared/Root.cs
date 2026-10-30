namespace PLang.Tests;

/// <summary>
/// The test process's root apps: each made from the one code the process holds for its wiring, so they read one scan of
/// plang's own types instead of each scanning again. The code is a shared one: it refuses a kind on one of its own
/// types (it would reach every test's app), so a test that loads a DLL whose kinds land on a code type makes its root
/// with a code of its own, <c>new global::app.@this(path)</c>. What a test's app adds otherwise is its own layer's.
/// </summary>
public static class Root
{
    // the code the process's root apps of each wiring are made from, made at its first ask
    private static readonly System.Lazy<global::app.origin.shared> _console = new(() => new(global::app.Wiring.console));
    private static readonly System.Lazy<global::app.origin.shared> _none = new(() => new(global::app.Wiring.none));

    /// <summary>A root app at <paramref name="path"/>, made from the process's code for <paramref name="wiring"/>.</summary>
    public static global::app.@this Make(string path, string? environment = null, global::app.Wiring wiring = global::app.Wiring.console)
        => new(path, (wiring == global::app.Wiring.none ? _none : _console).Value, environment);
}
