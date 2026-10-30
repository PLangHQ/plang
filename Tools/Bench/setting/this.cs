namespace Bench.setting;

/// <summary>What a run is asked for: the mode word, the kind of app (<c>root</c> or <c>child</c>) and the <c>BENCH_*</c>
/// environment, each read once.</summary>
internal sealed class @this
{
    internal @this(string[] args)
    {
        Mode = args.Length > 0 ? args[0] : "";
        Child = args.Length > 1 && args[1] == "child";
        Kind = Child ? "child" : "root";
    }

    /// <summary>The mode word: <c>birth</c>, <c>request</c>, …</summary>
    internal string Mode { get; }

    /// <summary>The app is a sub app made through the door by the User of one server-like app, a request app in a server.</summary>
    internal bool Child { get; }

    /// <summary>What the report names the app as: <c>root</c> (as the command line makes it) or <c>child</c>.</summary>
    internal string Kind { get; }

    /// <summary>The folder of the app (<c>BENCH_ROOT</c>; the repo's <c>test/</c> when not given).</summary>
    internal string Root { get; } = Environment.GetEnvironmentVariable("BENCH_ROOT")
        ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../test"));

    /// <summary>The goal a request app loads and runs (<c>BENCH_GOAL</c>; the condition test when not given).</summary>
    internal string Goal => Environment.GetEnvironmentVariable("BENCH_GOAL") ?? "/module/condition/if/basic/Condition.test.goal";

    /// <summary>How many times a mode repeats (<c>BENCH_N</c>), <paramref name="otherwise"/> when not given.</summary>
    internal int Count(int otherwise) => int.TryParse(Environment.GetEnvironmentVariable("BENCH_N"), out var count) ? count : otherwise;

    /// <summary>Seconds to rest before the last heap count (<c>BENCH_REST</c>); none when not given.</summary>
    internal int Rest => int.TryParse(Environment.GetEnvironmentVariable("BENCH_REST"), out var rest) ? rest : 0;

    /// <summary>How many bindings the maker holds (<c>BENCH_MAKER</c>): a debug watch, an on.event; none when not given.</summary>
    internal int Maker => int.TryParse(Environment.GetEnvironmentVariable("BENCH_MAKER"), out var count) ? count : 0;

    /// <summary>Births without the test session and the signing mock, so the bytes are the birth's alone (<c>BENCH_NOTEST=1</c>).</summary>
    internal bool Bare => Environment.GetEnvironmentVariable("BENCH_NOTEST") == "1";

    /// <summary>The goal is loaded as well as the app born (<c>BENCH_LOAD=1</c>).</summary>
    internal bool Load => Environment.GetEnvironmentVariable("BENCH_LOAD") == "1";

    /// <summary>The allocation ticks are reported too (<c>BENCH_TICKS=1</c>).</summary>
    internal bool Ticks => Environment.GetEnvironmentVariable("BENCH_TICKS") == "1";
}
