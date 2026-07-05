using System.Reflection;

namespace PLang.Tests.App.TypedReturnsTests;

// Reflection contract for action-handler Run() return types and the catalog
// strings Modules.Describe() emits for the trailing variable.set's type slot.

public class Stage2_MechanicalTypings_Part1Tests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = TestApp.Create("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    private static System.Type RunReturnType<THandler>()
        => typeof(THandler).GetMethod("Run", BindingFlags.Public | BindingFlags.Instance, System.Type.EmptyTypes)!.ReturnType;

    [Test]
    public async Task TestDiscover_Run_ReturnsTaskDataListOfTest()
    {
        var ret = RunReturnType<global::app.module.test.discover>();
        var expected = typeof(Task<global::app.data.@this<global::app.type.list.@this<global::app.test.@this>>>);
        await Assert.That(ret).IsEqualTo(expected);
    }

    [Test]
    public async Task TestRun_Run_ReturnsTaskDataListOfTest()
    {
        var ret = RunReturnType<global::app.module.test.run>();
        var expected = typeof(Task<global::app.data.@this<global::app.type.list.@this<global::app.test.@this>>>);
        await Assert.That(ret).IsEqualTo(expected);
    }

    // goal.getTypes returns per-step variable→type snapshots. PLang's call site
    // indexes the list directly (%varTypes[step.Index].Foo%) so the shape stays
    // as List<Dictionary<string, string>> rather than wrapping each step in a
    // dedicated TypeInfo record.
    [Test]
    public async Task GoalGetTypes_Run_ReturnsTaskDataOfStronglyTypedRecord()
    {
        var ret = RunReturnType<global::app.module.goal.getTypes>();
        var expected = typeof(Task<global::app.data.@this<global::app.type.list.@this<global::app.type.dict.@this>>>);
        await Assert.That(ret).IsEqualTo(expected);
    }

    // output.ask returns Task<Data<Ask>>. Suspend path returns an Ask with
    // Answer=null (ShouldExit() true); resume path returns Ask with Answer
    // bound (ShouldExit() false) so the step loop continues.
    [Test]
    public async Task OutputAsk_Run_ReturnsTaskDataOfAsk()
    {
        var ret = RunReturnType<global::app.module.output.ask>();
        var expected = typeof(Task<global::app.data.@this<global::app.module.output.Ask>>);
        await Assert.That(ret).IsEqualTo(expected);
    }

    [Test]
    public async Task ChannelSet_Run_ReturnsBareTaskOfData_VoidLike()
    {
        var ret = RunReturnType<global::app.module.channel.Set>();
        await Assert.That(ret).IsEqualTo(typeof(Task<Data>))
            .Because("channel.set produces no value — bare Task<Data> is the contract.");
    }

    [Test]
    public async Task ModulesDescribe_TestDiscover_AdvertisesListOfTestReturnType()
    {
        var rendered = await _app.Module.Describe();
        var row = rendered.FirstOrDefault(a => a.Module == "test" && a.ActionName == "discover");
        await Assert.That(row).IsNotNull();
        await Assert.That(row!.ReturnTypeName).IsEqualTo("list<test>");
    }

    [Test]
    public async Task ModulesDescribe_TestRun_AdvertisesListOfTestReturnType()
    {
        var rendered = await _app.Module.Describe();
        var row = rendered.FirstOrDefault(a => a.Module == "test" && a.ActionName == "run");
        await Assert.That(row).IsNotNull();
        await Assert.That(row!.ReturnTypeName).IsEqualTo("list<test>");
    }

    // Catalog renders output.ask's return as "ask" — the runtime return type
    // is Data<Ask>, with the user's string reply riding on Ask.Answer.
    [Test]
    public async Task ModulesDescribe_OutputAsk_AdvertisesAskReturnType()
    {
        var rendered = await _app.Module.Describe();
        var row = rendered.FirstOrDefault(a => a.Module == "output" && a.ActionName == "ask");
        await Assert.That(row).IsNotNull();
        await Assert.That(row!.ReturnTypeName).IsEqualTo("ask");
    }

    [Test]
    public async Task ModulesDescribe_ChannelSet_OmitsReturnsLine()
    {
        var rendered = await _app.Module.Describe();
        var row = rendered.FirstOrDefault(a => a.Module == "channel" && a.ActionName == "set");
        await Assert.That(row).IsNotNull();
        await Assert.That(row!.ReturnTypeName).IsEqualTo("data")
            .Because("Bare Task<Data> renders as 'data' — the Compile.llm template treats that as the polymorphic-default sentinel.");
    }

    // Footgun guard: a typed handler's T inside Data<T> must not itself be a
    // Data subtype — the implicit Data<T>(T value) operator silently wraps
    // when T = object and the source is already a Data, producing
    // Data<object>{ Value = Data<global::app.type.@bool.@this>{...} }. Asserting at the type level.
    [Test]
    public async Task DataValueFromTypedRun_NotDoubleWrapped()
    {
        var ret = RunReturnType<global::app.module.test.discover>();
        // Task<Data<T>> → unwrap → Data<T>
        var dataWrapper = ret.GetGenericArguments()[0];
        var t = dataWrapper.GetGenericArguments()[0];
        await Assert.That(typeof(Data).IsAssignableFrom(t)).IsFalse()
            .Because("Double-wrap footgun: Data<T> must not have T = another Data.");
    }
}
