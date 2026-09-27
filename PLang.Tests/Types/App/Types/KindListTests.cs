namespace PLang.Tests.App.type.lists;

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
        var ctx = TestApp.SharedContext;
        var kinds = ctx.App.type.list.Kind("sha256").list(ctx).Items().Select(t => t.kind.Name).ToList();
        await Assert.That(kinds).IsEquivalentTo(new[] { "keccak256", "sha256" });
    }

    [Test] public async Task ItemKinds_AreJsonListDictAndReflection()
    {
        var ctx = TestApp.SharedContext;
        var kinds = ctx.App.type.list.Kind("json").list(ctx).Items().Select(t => t.kind.Name).ToList();
        await Assert.That(kinds).IsEquivalentTo(new[] { "json", "list", "dict", "*" });
    }

    [Test] public async Task AFormatKind_IsAKindOfItsFamily()
    {
        var ctx = TestApp.SharedContext;
        await Assert.That(ctx.App.type.list.Kind("md").type(ctx).Name).IsEqualTo("text");
        var kinds = ctx.App.type.list.Kind("md").list(ctx).Items().Select(t => t.kind.Name).ToList();
        await Assert.That(kinds).Contains("md");
        await Assert.That(kinds).Contains("csv");
    }
}
