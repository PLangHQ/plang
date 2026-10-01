namespace PLang.Tests.App.actions.list;

/// <summary>
/// An action names the property it reads and answers a new value of (<c>[Input]</c>) — the builder asks the catalog
/// action for it, never the handler. An action without one answers null.
/// </summary>
public class InputTests
{
    private readonly global::app.@this _app = new global::app.@this("/test").Testing();

    [After(Test)]
    public async Task Dispose() => await _app.DisposeAsync();

    [Test]
    public async Task ListQuery_TakesItsList_AsItsInput()
        => await Assert.That(_app.Module("list")["query"]!.Input?.Name).IsEqualTo("List");

    [Test]
    public async Task AnActionWithoutOne_HasNoInput()
        => await Assert.That(_app.Module("variable")["set"]!.Input).IsNull();
}
