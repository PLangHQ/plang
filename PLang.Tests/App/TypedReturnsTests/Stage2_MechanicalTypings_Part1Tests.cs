using System.Reflection;

namespace PLang.Tests.App.TypedReturnsTests;

// Stage 2 — Mechanical typings, part 1: test.*, goal.getTypes, output.ask, channel.set.
// Architect: .bot/typed-action-returns/architect/stages.md (Stage 2)
// Plan: .bot/typed-action-returns/architect/plan.md (A.1)
//
// Status snapshot at this commit:
//   ✅ test.discover    → Task<Data<List<Test.@this>>>
//   ✅ test.run         → Task<Data<Results>>
//   ✅ channel.set      → Task<Data> (already bare, satisfies the void-like contract)
//   ⏳ goal.getTypes    → still polymorphic; new TypeInfo record not yet authored
//   ⏳ output.ask       → blocked by IExitsGoal forwarding pattern (Ask sentinel
//      cannot flow through Task<Data<string>> via implicit From; coder note in
//      architect plan needs Ingi's call on whether to split IExitsGoal handling
//      out of output.ask or downgrade the contract to bare Task<Data>).

public class Stage2_MechanicalTypings_Part1Tests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    private static System.Type RunReturnType<THandler>()
        => typeof(THandler).GetMethod("Run", BindingFlags.Public | BindingFlags.Instance, System.Type.EmptyTypes)!.ReturnType;

    [Test]
    public async Task TestDiscover_Run_ReturnsTaskDataListOfTest()
    {
        var ret = RunReturnType<global::app.modules.test.discover>();
        var expected = typeof(Task<global::app.data.@this<List<global::app.tester.Test.@this>>>);
        await Assert.That(ret).IsEqualTo(expected);
    }

    [Test]
    public async Task TestRun_Run_ReturnsTaskDataOfResults()
    {
        var ret = RunReturnType<global::app.modules.test.run>();
        var expected = typeof(Task<global::app.data.@this<global::app.tester.Results>>);
        await Assert.That(ret).IsEqualTo(expected);
    }

    [Test]
    public async Task GoalGetTypes_Run_ReturnsTaskDataOfStronglyTypedRecord()
    {
        // Pending — goal.getTypes still returns bare Task<Data>; the TypeInfo
        // record (or equivalent) hasn't been authored yet.
        Assert.Fail("Pending: TypeInfo record not yet created; see file header.");
    }

    [Test]
    public async Task OutputAsk_Run_ReturnsTaskDataOfString()
    {
        // Pending — IExitsGoal Ask sentinel cannot cleanly flow through
        // Task<Data<string>>. Coder flagged for Ingi (file header).
        Assert.Fail("Pending: IExitsGoal forwarding design needs Ingi's call.");
    }

    [Test]
    public async Task ChannelSet_Run_ReturnsBareTaskOfData_VoidLike()
    {
        var ret = RunReturnType<global::app.modules.channel.Set>();
        await Assert.That(ret).IsEqualTo(typeof(Task<Data>))
            .Because("channel.set produces no value — bare Task<Data> is the contract.");
    }

    [Test]
    public async Task ModulesDescribe_TestDiscover_AdvertisesListOfTestReturnType()
    {
        var rendered = await _app.Modules.Describe();
        var row = rendered.FirstOrDefault(a => a.Module == "test" && a.ActionName == "discover");
        await Assert.That(row).IsNotNull();
        await Assert.That(row!.ReturnTypeName).IsEqualTo("list<test>");
    }

    [Test]
    public async Task ModulesDescribe_TestRun_AdvertisesResultsReturnType()
    {
        var rendered = await _app.Modules.Describe();
        var row = rendered.FirstOrDefault(a => a.Module == "test" && a.ActionName == "run");
        await Assert.That(row).IsNotNull();
        await Assert.That(row!.ReturnTypeName).IsEqualTo("results");
    }

    [Test]
    public async Task ModulesDescribe_OutputAsk_AdvertisesStringReturnType()
    {
        // Pending — blocked on output.ask typing.
        Assert.Fail("Pending: output.ask not yet typed.");
    }

    [Test]
    public async Task ModulesDescribe_ChannelSet_OmitsReturnsLine()
    {
        var rendered = await _app.Modules.Describe();
        var row = rendered.FirstOrDefault(a => a.Module == "channel" && a.ActionName == "set");
        await Assert.That(row).IsNotNull();
        await Assert.That(row!.ReturnTypeName).IsEqualTo("data")
            .Because("Bare Task<Data> renders as 'data' — the Compile.llm template treats that as the polymorphic-default sentinel.");
    }

    // Footgun guard: typed handlers must not return Data<Data<T>>. The Run()
    // signature is the contract — Task<Data<List<Test>>> means the runtime
    // instance is Data<List<Test>>, with .Value being the raw collection. A
    // double-wrap would mean Data<List<Test>> containing another Data — caught
    // at the type level by asserting the static return type matches Data<T>
    // where T is the collection itself, not another Data.
    [Test]
    public async Task DataValueFromTypedRun_NotDoubleWrapped()
    {
        var ret = RunReturnType<global::app.modules.test.discover>();
        // Task<Data<T>> → unwrap → Data<T>
        var dataWrapper = ret.GetGenericArguments()[0];
        var t = dataWrapper.GetGenericArguments()[0];
        await Assert.That(typeof(Data).IsAssignableFrom(t)).IsFalse()
            .Because("Double-wrap footgun: Data<T> must not have T = another Data.");
    }
}
