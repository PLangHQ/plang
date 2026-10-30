using System.Reflection;

namespace Bench.mode.closure;

/// <summary>SCRATCH (not committed): which classes hold a compiler closure of the name the allocation ticks showed
/// (BENCH_CLOSURE, e.g. <c>&lt;&gt;c__DisplayClass32_0</c>), with what each captures and the methods that make it.</summary>
internal sealed class @this(setting.@this setting) : mode.@this(setting)
{
    internal override Task<int> Run()
    {
        var name = Environment.GetEnvironmentVariable("BENCH_CLOSURE") ?? "<>c__DisplayClass32_0";
        foreach (var type in typeof(global::app.@this).Assembly.GetTypes().Where(t => t.Name == name))
        {
            var fields = string.Join(", ", type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(f => $"{f.FieldType.Name} {f.Name}"));
            var lambdas = string.Join(", ", type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(m => m.Name.StartsWith('<')).Select(m => m.Name));
            Say($"{type.DeclaringType?.FullName}: captures [{fields}] lambdas [{lambdas}]");
        }
        return Task.FromResult(0);
    }
}
