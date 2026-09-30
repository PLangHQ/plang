using System.Reflection;

namespace PLang.Tests.App.TypedReturnsTests;

// Reflection contract for action-handler Start() return types and the catalog
// strings Modules.Describe() emits for the trailing variable.set's type slot.

public class Stage2_MechanicalTypings_Part1Tests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/app").Testing();

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    private static System.Type StartReturnType<THandler>()
        => typeof(THandler).GetMethod("Start", BindingFlags.Public | BindingFlags.Instance, System.Type.EmptyTypes)!.ReturnType;

    [Test]
    public async Task TestDiscover_Run_ReturnsTaskDataListOfTest()
    {
        var ret = StartReturnType<global::app.module.test.discover>();
        var expected = typeof(Task<global::app.data.@this<global::app.type.item.list.@this<global::app.test.@this>>>);
        await Assert.That(ret).IsEqualTo(expected);
    }

    [Test]
    public async Task TestStart_Start_ReturnsTaskDataListOfTest()
    {
        var ret = StartReturnType<global::app.module.test.start>();
        var expected = typeof(Task<global::app.data.@this<global::app.type.item.list.@this<global::app.test.@this>>>);
        await Assert.That(ret).IsEqualTo(expected);
    }

    // output.ask relays the answer as it comes — the user's data, or a pending Ask on the suspend path — so it is
    // a polymorphic forwarder: bare Task<Data>.
    [Test]
    public async Task OutputAsk_Run_ReturnsBareTaskOfData()
    {
        var ret = StartReturnType<global::app.module.output.ask>();
        await Assert.That(ret).IsEqualTo(typeof(Task<Data>));
    }

    [Test]
    public async Task ChannelSet_Run_ReturnsBareTaskOfData_VoidLike()
    {
        var ret = StartReturnType<global::app.module.channel.Set>();
        await Assert.That(ret).IsEqualTo(typeof(Task<Data>))
            .Because("channel.set produces no value — bare Task<Data> is the contract.");
    }

    [Test]
    public async Task ModulesDescribe_TestDiscover_AdvertisesListOfTestReturnType()
    {
        var row = _app.Module("test")["discover"];
        await Assert.That(row).IsNotNull();
        await Assert.That(row!.Return).IsEqualTo(_app.type.list[new global::app.type.@this("list", "test"), _app.actor.list.User.Context]);
    }

    [Test]
    public async Task ModulesDescribe_TestStart_AdvertisesListOfTestReturnType()
    {
        var row = _app.Module("test")["start"];
        await Assert.That(row).IsNotNull();
        await Assert.That(row!.Return).IsEqualTo(_app.type.list[new global::app.type.@this("list", "test"), _app.actor.list.User.Context]);
    }

    // Catalog renders output.ask's return as "item" — the answer is whatever the user's data is.
    [Test]
    public async Task ModulesDescribe_OutputAsk_AdvertisesItemReturnType()
    {
        var row = _app.Module("output")["ask"];
        await Assert.That(row).IsNotNull();
        await Assert.That(row!.Return).IsEqualTo(_app.type.list["item"]);
    }

    [Test]
    public async Task ModulesDescribe_ChannelSet_OmitsReturnsLine()
    {
        var row = _app.Module("channel")["set"];
        await Assert.That(row).IsNotNull();
        await Assert.That(row!.Return).IsEqualTo(_app.type.list["item"])
            .Because("An undefined T is the unconstrained plang type item, C#'s object.");
    }

    // Footgun guard: a typed handler's T inside Data<T> must not itself be a
    // Data subtype — the implicit Data<T>(T value) operator silently wraps
    // when T = object and the source is already a Data, producing
    // Data<object>{ Value = Data<global::app.type.item.@bool.@this>{...} }. Asserting at the type level.
    [Test]
    public async Task DataValueFromTypedRun_NotDoubleWrapped()
    {
        var ret = StartReturnType<global::app.module.test.discover>();
        // Task<Data<T>> → unwrap → Data<T>
        var dataWrapper = ret.GetGenericArguments()[0];
        var t = dataWrapper.GetGenericArguments()[0];
        await Assert.That(typeof(Data).IsAssignableFrom(t)).IsFalse()
            .Because("Double-wrap footgun: Data<T> must not have T = another Data.");
    }
}
