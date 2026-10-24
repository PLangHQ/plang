namespace PLang.Tests.App.Types;

// json content is one value: anything after it means the characters are no json, never a value with the rest dropped.
public class JsonTrailingContentTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private global::app.data.@this? Opened(string characters)
        => app.type.list["item"].kind["json"]!.Open(characters, app.actor.list.User.Context);

    [Test]
    [Arguments("1, \"evil\": 2")]
    [Arguments("{\"a\": 1} {\"b\": 2}")]
    [Arguments("[1] , 2")]
    public async Task ContentAfterTheFirstValue_IsRefused(string characters)
    {
        var opened = Opened(characters);

        await Assert.That(opened).IsNotNull();
        await Assert.That(opened!.Success).IsFalse();
    }

    [Test]
    [Arguments("1")]
    [Arguments("{\"a\": 1}  \n")]
    [Arguments("  [1, 2]")]
    public async Task OneValue_WithSpaceAroundIt_Reads(string characters)
        => await Assert.That(Opened(characters)!.Success).IsTrue();
}
