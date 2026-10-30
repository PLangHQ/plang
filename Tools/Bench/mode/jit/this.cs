using System.Diagnostics.Tracing;

namespace Bench.mode.jit;

/// <summary>One fresh process, an app born, the goal loaded and run once — which methods the JIT compiled, grouped by the
/// namespace they belong to, with the time each group took (from the runtime's own JIT events; the listener adds its own
/// cost, so read the shares, not the milliseconds).</summary>
internal sealed class @this(setting.@this setting) : mode.@this(setting)
{
    internal override async Task<int> Run()
    {
        using var listener = new Compiled();
        var app = Born.Request();
        var goal = new Bench.goal.@this(app, Setting.Goal);
        var loaded = await goal.Load();
        await goal.Start(loaded);
        await app.DisposeAsync();
        listener.Report(Say);
        return 0;
    }

    private sealed class Compiled : EventListener
    {
        private readonly Dictionary<long, (string Group, string Name, DateTime At)> _started = new();
        private readonly Dictionary<string, (int Methods, double Ms)> _groups = new();
        private readonly Dictionary<string, double> _slow = new();

        protected override void OnEventSourceCreated(EventSource source)
        {
            if (source.Name == "Microsoft-Windows-DotNETRuntime")
                EnableEvents(source, EventLevel.Verbose, (EventKeywords)0x10);   // JIT
        }

        protected override void OnEventWritten(EventWrittenEventArgs e)
        {
            if (e.Payload == null) return;
            var names = e.PayloadNames!;
            string Text(string key) => names.IndexOf(key) is var at and >= 0 ? e.Payload[at] as string ?? "" : "";
            long Id() => names.IndexOf("MethodID") is var at and >= 0 ? Convert.ToInt64(e.Payload[at]) : 0;
            if (e.EventName?.StartsWith("MethodJittingStarted") == true)
            {
                var ns = Text("MethodNamespace");
                lock (_started) _started[Id()] = (Group(ns), ns + "." + Text("MethodName"), e.TimeStamp);
            }
            else if (e.EventName?.StartsWith("MethodLoadVerbose") == true)
            {
                lock (_started)
                {
                    if (!_started.Remove(Id(), out var begun)) return;
                    var ms = (e.TimeStamp - begun.At).TotalMilliseconds;
                    _groups[begun.Group] = _groups.TryGetValue(begun.Group, out var g) ? (g.Methods + 1, g.Ms + ms) : (1, ms);
                    _slow[begun.Name] = ms;
                }
            }
        }

        // the first four segments of the namespace: app.type.item.number → "app.type.item.number"; System.* by two
        private static string Group(string ns)
        {
            var parts = ns.Split('.');
            var depth = parts.Length > 0 && parts[0] == "app" ? 4 : 2;
            return string.Join('.', parts.Take(depth));
        }

        internal void Report(Action<FormattableString> say)
        {
            Thread.Sleep(1500);
            lock (_started)
            {
                var methods = _groups.Values.Sum(g => g.Methods);
                var ms = _groups.Values.Sum(g => g.Ms);
                say($"JIT compiled {methods} methods, {ms:F0} ms of JIT in all (events overlap, so a sum can pass the wall time)");
                foreach (var (group, g) in _groups.OrderByDescending(p => p.Value.Ms).Take(28))
                    say($"  {g.Ms,7:F1} ms  {g.Methods,5} methods  {group}");
                say($"the 12 slowest single methods:");
                foreach (var (name, time) in _slow.OrderByDescending(p => p.Value).Take(12))
                    say($"  {time,7:F1} ms  {name}");
            }
        }
    }
}
