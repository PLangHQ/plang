namespace PLang.Tests.App.Types;

// A kind's class declares the type it is a kind of; a kind owns its aliases; a type's kinds are
// answered as full types.
public class KindListTests
{
    [Test] public async Task NumberKind_IsAKindOfNumber()
    {
        var ctx = TestApp.SharedContext;
        await Assert.That(ctx.App.type.list.Kind("int").type(ctx).Name).IsEqualTo("number");
    }

    [Test] public async Task Integer_AnswersTheIntKind()
    {
        var ctx = TestApp.SharedContext;
        await Assert.That(ctx.App.type.list.Kind("integer").Name).IsEqualTo("int");
        var type = ctx.App.type.list[new global::app.type.@this("number", "integer"), ctx];
        await Assert.That(type.kind.Name).IsEqualTo("int");
    }

    [Test] public async Task NumberKindList_IsEveryPrecision_AsFullTypes()
    {
        var ctx = TestApp.SharedContext;
        var kinds = ctx.App.type.list.Kind("int").list(ctx).Items().Select(t => $"{t.Name}/{t.kind.Name}").ToList();
        await Assert.That(kinds).Contains("number/int");
        await Assert.That(kinds).Contains("number/decimal");
        await Assert.That(kinds).Contains("number/biginteger");
        await Assert.That(kinds.All(k => k.StartsWith("number/"))).IsTrue();
    }

    [Test] public async Task HashKindList_IsItsAlgorithms()
    {
        // sha256 is both hash's algorithm and binary's checksum file — a kind is its type's, so ask hash.
        var ctx = TestApp.SharedContext;
        var kinds = ctx.App.type.list["hash"].kind["sha256"]!.list(ctx).Items().Select(t => t.kind.Name).ToList();
        await Assert.That(kinds).IsEquivalentTo(new[] { "keccak256", "sha256" });
    }

    [Test] public async Task AKindNameTwoTypesHold_IsAmbiguousByBareName()
    {
        var ctx = TestApp.SharedContext;
        await Assert.That(() => ctx.App.type.list.Kind("sha256")).Throws<InvalidOperationException>();
        await Assert.That(ctx.App.type.list["binary"].kind["sha256"]!.type(ctx).Name).IsEqualTo("binary");
    }

    [Test] public async Task ItemKinds_AreJsonListDictAndReflection()
    {
        var ctx = TestApp.SharedContext;
        var kinds = ctx.App.type.list.Kind("json").list(ctx).Items().Select(t => t.kind.Name).ToList();
        await Assert.That(kinds).IsEquivalentTo(new[] { "json", "list", "dict", "*" });
    }

    [Test] public async Task AFormatKind_IsAKindOfTheTypeThatReadsIt()
    {
        var ctx = TestApp.SharedContext;
        await Assert.That(ctx.App.type.list.Kind("md").type(ctx).Name).IsEqualTo("text");
        var kinds = ctx.App.type.list.Kind("md").list(ctx).Items().Select(t => t.kind.Name).ToList();
        await Assert.That(kinds).Contains("md");
        // csv is table's: table reads it
        await Assert.That(kinds).DoesNotContain("csv");
        await Assert.That(ctx.App.type.list.Kind("csv").type(ctx).Name).IsEqualTo("table");
    }
}
