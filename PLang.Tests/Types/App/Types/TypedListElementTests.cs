namespace PLang.Tests.App.Types;

/// <summary>
/// A typed list (<c>list&lt;path&gt;</c>) holds its rows as they came in and knows its kind; a row is made its element
/// type's value at each read — by index or by walking — and nothing is written back, so a list read and saved untouched
/// is unchanged. Its count makes nothing, and a row that makes no path fails where it is read. Nothing fires for an
/// element: the list's birth is the event, its elements are part of it.
/// </summary>
public class TypedListElementTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/tmp/typed-list-" + System.Guid.NewGuid().ToString("N")[..8]).Testing();

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;

    private async Task<global::app.data.@this> Files(params object?[] raw)
        => await Ctx.App.type.list[new global::app.type.@this("list", "path"), Ctx].Create(raw.ToList(), Ctx);

    private static global::app.type.item.list.@this List(object? held) => (global::app.type.item.list.@this)held!;

    [Test]
    public async Task AnElement_IsBornAPath_AtItsRead()
    {
        var files = await Files("a.txt", "b.txt");

        var first = await files.Get("0");

        await first.IsSuccess();
        await Assert.That(await first.Value()).IsAssignableTo<path>();
        await Assert.That(first.Type.Name).IsEqualTo("path");
    }

    [Test]
    public async Task ASecondRead_MakesThePathAgain()
    {
        var files = await Files("a.txt", "b.txt");

        var once = await (await files.Get("0")).Value();
        var again = await (await files.Get("0")).Value();

        await Assert.That(again).IsAssignableTo<path>();
        await Assert.That(again).IsNotSameReferenceAs(once);
    }

    [Test]
    public async Task WalkingTheList_MakesEachElement_AndWritesNothingBack()
    {
        var files = await Files("a.txt", "b.txt");
        var list = List(await files.Value());

        var walked = list.Items(Ctx).Select(e => e.Peek()).ToList();

        await Assert.That(walked.All(e => e is path)).IsTrue();
        await Assert.That(list.Slots().All(slot => slot is string)).IsTrue();
    }

    [Test]
    public async Task AListReadAndSavedUntouched_IsUnchanged()
    {
        var files = await Files("a.txt", "b.txt");
        var list = List(await files.Value());
        await files.Get("0");

        using var stream = new System.IO.MemoryStream();
        await (await Ctx.App.type.list.Mime("application/json").Encode(stream, files, Ctx)).IsSuccess();

        await Assert.That(list.Slots().All(slot => slot is string)).IsTrue();
    }

    [Test]
    public async Task Counting_BirthsNothing()
    {
        var files = await Files("a.txt", "b.txt");
        var list = List(await files.Value());

        await Assert.That(list.CountRaw).IsEqualTo(2);

        await Assert.That(list.Slots().First()).IsTypeOf<string>();
    }

    [Test]
    public async Task AnElementThatMakesNoPath_FailsWhereItIsRead()
    {
        var files = await Files("a.txt", 42L);

        await (await files.Get("0")).IsSuccess();
        var bad = await files.Get("1");

        await bad.IsFailure();
    }
}
