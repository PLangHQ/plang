namespace PLang.Tests.App.Serialization;

// parallel reads back as itself through the wire: the formal line the builder writes is read into a step (as
// build.match reads it), saved in a .pr and loaded again, and the loop's Parallel is what was written.
public class ParallelWireTests
{
    private static string RepoRoot()
    {
        var dir = System.AppContext.BaseDirectory;
        while (dir != null && !System.IO.Directory.Exists(System.IO.Path.Combine(dir, "PLang", "app")))
            dir = System.IO.Directory.GetParent(dir)?.FullName;
        return dir!;
    }

    private static async Task<global::app.type.item.parallel.@this> RoundTrip(string parallel)
    {
        await using var os = new global::app.@this(System.IO.Path.Combine(RepoRoot(), "os")).Testing().Building();
        var context = os.actor.list.User.Context;
        var goal = global::app.goal.@this.Parse("Start\n- foreach %orders% in parallel, call Ship\n",
            global::app.type.item.path.@this.Resolve("/Start.goal", context), context)!;
        var read = new global::app.goal.step.action.formal.Reader(goal.Step[0], context.App.module.list)
            .Read($"loop.foreach(Collection=%orders%, Parallel={parallel}); goal.call(Name=\"Ship\")", context);
        await read.IsSuccess();
        goal.Step[0].Code = (global::app.goal.step.action.list.@this)read.Peek()!;

        var loaded = await RealGoalLoad.Read(os, await context.Pr(goal));
        var foreach_ = loaded.Step[0].Code[0];
        var value = await foreach_.Property["Parallel"]!.Data(context).Value();
        return (global::app.type.item.parallel.@this)value!;
    }

    [Test]
    public async Task True_ReadsBackAsParallelAtTheMachinesDefault()
    {
        var parallel = await RoundTrip("true");
        await Assert.That(parallel.IsTruthy()).IsTrue();
        await Assert.That(parallel.Cpu.ToInt64()).IsEqualTo(System.Math.Max(1, (long)(System.Environment.ProcessorCount * 0.8)));
    }

    [Test]
    public async Task AnEmptyParallel_ReadsBackAtTheMachinesDefault()
    {
        var parallel = await RoundTrip("{}");
        await Assert.That(parallel.IsTruthy()).IsTrue();
        await Assert.That(parallel.Cpu.ToInt64()).IsEqualTo(System.Math.Max(1, (long)(System.Environment.ProcessorCount * 0.8)));
    }

    [Test]
    public async Task ACpu_ReadsBackAsThatManyAtOnce()
    {
        var parallel = await RoundTrip("{cpu: 2}");
        await Assert.That(parallel.IsTruthy()).IsTrue();
        await Assert.That(parallel.Cpu.ToInt64()).IsEqualTo(2L);
    }

    [Test]
    public async Task False_ReadsBackAsNotParallel()
        => await Assert.That((await RoundTrip("false")).IsTruthy()).IsFalse();

    // a parallel value written to the wire (plang's store, a snapshot) and read back is itself: off stays off, a cpu
    // stays that cpu, and none given stays the default of the machine that reads it
    private static async Task<global::app.type.item.parallel.@this> Relayed(global::app.type.item.parallel.@this parallel)
    {
        await using var app = new global::app.@this("/app").Testing();
        var context = app.actor.list.User.Context;
        var plang = app.type.list.Kind("plang");
        using var stream = new System.IO.MemoryStream();
        await (await plang.Encode(stream, new global::app.data.@this("p", parallel, context: context), context, global::app.View.Store)).IsSuccess();
        var read = await plang.Decode(stream.ToArray(), context, view: global::app.View.Store);
        await read.IsSuccess();
        return (global::app.type.item.parallel.@this)(await read.Value())!;
    }

    [Test]
    public async Task AParallelValue_RelayedThroughTheWire_IsItself()
    {
        await Assert.That((await Relayed(global::app.type.item.parallel.@this.Off)).IsTruthy()).IsFalse();
        await Assert.That((await Relayed(new global::app.type.item.parallel.@this(2))).Cpu.ToInt64()).IsEqualTo(2L);
        var machine = await Relayed(new global::app.type.item.parallel.@this());
        await Assert.That(machine.IsTruthy()).IsTrue();
        await Assert.That(machine.Cpu.ToInt64()).IsEqualTo(System.Math.Max(1, (long)(System.Environment.ProcessorCount * 0.8)));
    }
}
