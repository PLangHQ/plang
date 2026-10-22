using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.Runtime.App.Types;

/// Security (item-doors, L3): an unread <c>{item, json}</c> source caches its parse (kind/json/source.cs) — "it holds
/// no context: every asker uses it with their own". The parse builds Data holding the FIRST opener's context. If the
/// System actor opens it first (a truth asked, a comparison), a later user-actor read must still resolve against the
/// USER, never System. Each test asserts that: red if a leaf is born with / resolves as the first opener.
public class JsonSourceContextTests : System.IAsyncDisposable
{
    private readonly string root;
    private readonly global::app.@this app;

    public JsonSourceContextTests()
    {
        root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-sec-g-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        app = new global::app.@this(root).TestSigning();
    }
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private global::app.actor.context.@this System_ => app.actor.list.System.Context;
    private global::app.actor.context.@this User => app.actor.list.User.Context;

    // an unread source the way a json body rides: made by the json kind, opened by NO ONE yet
    private global::app.type.item.kind.json.source Unread(string json)
        => (global::app.type.item.kind.json.source)(app.type.list[new global::app.type.@this("item", "json"), System_].kind.Load(
            System.Text.Encoding.UTF8.GetBytes(json), System_).AsTask().GetAwaiter().GetResult())!.Peek()!;

    // the System opens it first (a truth asked: the parse is cached), then the user reads the dict it stands for
    private async Task<global::app.data.@this> LeafAsTheUser(string json, string key)
    {
        var source = Unread(json);
        await source.AsBooleanAsync(System_);                         // System opens it: the cached parse is System's
        var holder = new global::app.data.@this("j", source, context: User);
        var opened = await holder.Get(key);                            // the user navigates into it
        return opened;
    }

    [Test]
    public async Task ALeafTheSystemOpened_IsBornKnowingTheUserWhoReadsIt()
    {
        var leaf = await LeafAsTheUser("""{"p": "/etc/passwd"}""", "p");
        await Assert.That(leaf.Context.Actor.Name).IsEqualTo(User.Actor.Name);
    }

    [Test]
    public async Task ALeafTheSystemOpened_AsAPath_IsAuthorizedAsTheUser_NotAsSystem()
    {
        // the stores' folder tells the actors apart: the System holds a standing grant there, the user is refused
        var leaf = await LeafAsTheUser("""{"p": "/.data/setting/data.sqlite"}""", "p");
        // the path a user goal now reads is checked as the user — System's standing grant must not apply
        var path = await leaf.Value<global::app.type.item.path.@this>();
        var auth = await path!.Authorize(global::app.type.item.permission.Verb.read, leaf.Context);
        await Assert.That(leaf.Context.Actor.Name).IsEqualTo(User.Actor.Name);
        await Assert.That(auth.Success).IsFalse();
    }

    [Test]
    public async Task ALeafTheSystemOpened_ResolvesAVariableInTheUsersMemory_NotTheSystems()
    {
        await System_.Variable.Set("who", "system-secret");
        await User.Variable.Set("who", "user-value");
        var leaf = await LeafAsTheUser("""{"v": "%who%"}""", "v");
        var text = (await leaf.Value())?.ToString();
        await Assert.That(text).IsNotEqualTo("system-secret");
    }
}
