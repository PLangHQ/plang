namespace PLang.Tests.App.Types;

// A kind's class declares the type it is a kind of; a kind owns its aliases; a type's kinds are
// answered as full types.
public class KindListTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    [Test] public async Task NumberKind_IsAKindOfNumber()
    {
        var ctx = app.actor.list.User.Context;
        await Assert.That(ctx.App.type.list["number"].kind["int"]!.type(ctx).Name).IsEqualTo("number");
    }

    [Test] public async Task Integer_AnswersTheIntKind()
    {
        var ctx = app.actor.list.User.Context;
        await Assert.That(ctx.App.type.list["number"].kind["integer"]!.Name).IsEqualTo("int");
        var type = ctx.App.type.list[new global::app.type.@this("number", "integer"), ctx];
        await Assert.That(type.kind.Name).IsEqualTo("int");
    }

    [Test] public async Task NumberKindList_IsEveryPrecision_AsFullTypes()
    {
        var ctx = app.actor.list.User.Context;
        var kinds = ctx.App.type.list["number"].kind["int"]!.list(ctx).Items().Select(t => $"{t.Name}/{t.kind.Name}").ToList();
        await Assert.That(kinds).Contains("number/int");
        await Assert.That(kinds).Contains("number/decimal");
        await Assert.That(kinds).Contains("number/biginteger");
        await Assert.That(kinds.All(k => k.StartsWith("number/"))).IsTrue();
    }

    [Test] public async Task HashKindList_IsItsAlgorithms()
    {
        // sha256 is both hash's algorithm and binary's checksum file — a kind is its type's, so ask hash.
        var ctx = app.actor.list.User.Context;
        var kinds = ctx.App.type.list["hash"].kind["sha256"]!.list(ctx).Items().Select(t => t.kind.Name).ToList();
        await Assert.That(kinds).IsEquivalentTo(new[] { "keccak256", "sha256" });
    }

    [Test] public async Task AKindNameTwoTypesHold_IsAnsweredByEachParent()
    {
        // sha256 is a kind of two types (hash's algorithm, binary's checksum file). Asked of its parent
        // no name is ambiguous — each type answers its own.
        var ctx = app.actor.list.User.Context;
        await Assert.That(ctx.App.type.list["binary"].kind["sha256"]!.type(ctx).Name).IsEqualTo("binary");
        await Assert.That(ctx.App.type.list["hash"].kind["sha256"]!.type(ctx).Name).IsEqualTo("hash");
    }

    [Test] public async Task HostKinds_AreInternal_JsonIsOffered()
    {
        // item's dict/list/* are C# host carriers, never types a program names: Internal, so the builder's
        // kind follow-up leaves them out (814). A kinded type answers Internal through its kind. json is offered.
        var ctx = app.actor.list.User.Context;
        var internalByKind = ctx.App.type.list["item"].kind.list(ctx).Items()
            .ToDictionary(k => ((global::app.type.@this)k).kind.Name, k => ((global::app.type.@this)k).Internal);
        await Assert.That(internalByKind["dict"]).IsTrue();
        await Assert.That(internalByKind["list"]).IsTrue();
        await Assert.That(internalByKind["*"]).IsTrue();
        await Assert.That(internalByKind["json"]).IsFalse();
    }

    [Test] public async Task ItemKinds_AreJsonListDictAndReflection()
    {
        var ctx = app.actor.list.User.Context;
        var kinds = ctx.App.type.list["item"].kind["json"]!.list(ctx).Items().Select(t => t.kind.Name).ToList();
        await Assert.That(kinds).IsEquivalentTo(new[] { "json", "list", "dict", "*" });
    }

    [Test] public async Task AFormatKind_IsAKindOfTheTypeThatReadsIt()
    {
        var ctx = app.actor.list.User.Context;
        await Assert.That(ctx.App.type.list["text"].kind["md"]!.type(ctx).Name).IsEqualTo("text");
        var kinds = ctx.App.type.list["text"].kind["md"]!.list(ctx).Items().Select(t => t.kind.Name).ToList();
        await Assert.That(kinds).Contains("md");
        // csv is table's: table reads it
        await Assert.That(kinds).DoesNotContain("csv");
        await Assert.That(ctx.App.type.list["table"].kind["csv"]!.type(ctx).Name).IsEqualTo("table");
    }
}
