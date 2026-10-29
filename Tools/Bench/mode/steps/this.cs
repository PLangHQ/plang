namespace Bench.mode.steps;

/// <summary>The bytes of each step of a test app's making, averaged over BENCH_N births (this thread's allocations).</summary>
internal sealed class @this(setting.@this setting) : mode.@this(setting)
{
    private static readonly string[] Names =
        ["new app", "test.Open", "new TestSigning", "Register ISigning", "Register IKey", "SetDefault ISigning", "SetDefault IKey", "dispose"];

    internal override async Task<int> Run()
    {
        var count = Setting.Count(2000);
        var sums = new long[Names.Length];
        for (var i = 0; i < 20 + count; i++)
        {
            var lap = new meter.@this();
            var bytes = new List<long>();
            var app = Born.Make(true); bytes.Add(lap.Lap());
            app.test.list.Open(app.actor.list.User); bytes.Add(lap.Lap());
            var provider = new global::PLang.Tests.Shared.TestSigning { IsBuiltIn = true }; bytes.Add(lap.Lap());
            app.Code.Register<global::app.module.signing.code.ISigning>(provider); bytes.Add(lap.Lap());
            app.Code.Register<global::app.module.signing.code.IKey>(provider); bytes.Add(lap.Lap());
            app.Code.SetDefault<global::app.module.signing.code.ISigning>("test-signing"); bytes.Add(lap.Lap());
            app.Code.SetDefault<global::app.module.signing.code.IKey>("test-signing"); bytes.Add(lap.Lap());
            await app.DisposeAsync(); bytes.Add(lap.Lap());
            if (i >= 20) for (var step = 0; step < Names.Length; step++) sums[step] += bytes[step];
        }
        for (var step = 0; step < Names.Length; step++)
            Say($"{sums[step] / (double)count:F0} B  {Names[step]}");
        return 0;
    }
}
