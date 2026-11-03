// The fast-apps benchmark: what an app costs to make and run. Tools/Bench/run.sh runs it with everything else it
// measures; this is the part that needs the app's internals.
//
//   Bench <mode> <root|child>
//
//   birth      an app's birth: 20 warm, then 100 timed, each disposed before the next
//   request    a request app BENCH_N (3,000) times: born, a goal loaded and run, disposed
//   steps      the bytes of each step of a test app's making
//   index      the bytes of one action.Index over the request goal's actions
//   events     the bytes of the events' first ask, and of the goal's load and run after
//   alloc      allocation ticks by type over BENCH_N births
//   phases     a request app's bytes by phase: born, loaded, run, disposed
//   cold       one fresh process: the first of everything and the JIT's share, then the same again
//   loadtypes  allocation ticks by type of a birth, and with BENCH_LOAD=1 of its goal's load
//   repeat     one app and one load, the goal run BENCH_N times: the bytes per run and the GC's share
//   resolve    what shell.Resolve of a set action is made of, best of 7 rounds each
//   jit        one fresh process: the methods the JIT compiled, by namespace, with each group's time
//   copy       what data.Copy costs for a value with no bag, an empty bag, and a property
//   closure    which classes hold the compiler closure BENCH_CLOSURE names, and what each captures
//
// Env: BENCH_ROOT (the folder of the app; default <repo>/test), BENCH_N, BENCH_REST (seconds to rest before the
// last heap count; 0 = none), BENCH_MAKER (bindings the maker holds), BENCH_NOTEST, BENCH_LOAD, BENCH_TICKS. One line
// on stdout per run, `key=value` pairs, which run.sh reads.
//
// root: an app as the command line makes it (`new app.@this(path)`). child: made through the door by the User of
// one server-like app made once (`new app.@this(path, actor)`) — a request app in a server.
var setting = new Bench.setting.@this(args);
if (Bench.mode.list.@this.Find(setting) is not { } mode)
{
    Console.Error.WriteLine("usage: Bench <birth|request|steps|index|events|alloc|phases|cold|loadtypes|repeat|resolve|jit|copy|closure> <root|child>");
    return 2;
}
return await mode.Run();
