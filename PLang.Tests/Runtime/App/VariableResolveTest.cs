using Code = global::app.type.item.variable.code;

namespace PLang.Tests.App;

public class VariableResolveTest : System.IAsyncDisposable
{
    private readonly global::app.@this _app = global::PLang.Tests.TestApp.Create("/tmp/varresolve-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    private global::app.type.item.variable.@this Resolve(string raw)
        => global::app.type.item.variable.@this.Resolve(raw, _app.actor.list.User.Context);

    private static string[] Hops(global::app.type.item.variable.@this v)
        => v.Code.Items().Select(h => $"{h.Kind}:{h.Text}").ToArray();

    [Test] public async Task Resolve_Bang_IsARootAndABindingProperty()
    {
        var v = Resolve("%response!cost%");
        await Assert.That(Hops(v)).IsEquivalentTo(new[] { "variable:response", "property:!cost" });
        await Assert.That(((Code.Property)v.Code[1]).Name).IsEqualTo("!cost");
    }

    [Test] public async Task Resolve_BangRoot_KeepsBangInRootName()
    {
        var v = Resolve("%!flag%");
        await Assert.That(v.Code.Root.Name).IsEqualTo("!flag");
        await Assert.That(v.Code.Count).IsEqualTo(1);
    }

    [Test] public async Task Resolve_BangRootWithBangProperty_IsTwoHops()
    {
        var v = Resolve("%!response!cost%");
        await Assert.That(Hops(v)).IsEquivalentTo(new[] { "variable:!response", "property:!cost" });
    }

    [Test] public async Task Resolve_ChainedBang_IsAHopEach()
    {
        var v = Resolve("%x!a!b%");
        await Assert.That(Hops(v)).IsEquivalentTo(new[] { "variable:x", "property:!a", "property:!b" });
    }

    [Test] public async Task Resolve_BangAfterDot_ReadsTheChildsBinding()
    {
        var v = Resolve("%x.kind!cost%");
        await Assert.That(Hops(v)).IsEquivalentTo(new[] { "variable:x", "property:.kind", "property:!cost" });
    }

    [Test] public async Task Resolve_DoubleBang_IsNotAVariable()
    {
        await Assert.That(() => Resolve("%x!!cost%")).Throws<global::app.error.AppException>();
    }

    [Test] public async Task Resolve_EmptyPropertyKey_IsNotAVariable()
    {
        await Assert.That(() => Resolve("%x!%")).Throws<global::app.error.AppException>();
    }

    [Test] public async Task Resolve_PlainPath_IsAHopEach()
    {
        var v = Resolve("%planStep.actions%");
        await Assert.That(v.Name).IsEqualTo("planStep.actions");
        await Assert.That(Hops(v)).IsEquivalentTo(new[] { "variable:planStep", "property:.actions" });
    }

    [Test] public async Task VariableSet_BangSyntax_WritesProperty()
    {
        await using var app = TestApp.Create("/tmp/var-set-bang-" + System.Guid.NewGuid().ToString("N")[..8]);
        var context = app.actor.list.User.Context;

        await app.Run<global::app.module.variable.Set>(new global::app.module.variable.Set(app.actor.list.User.Context)
        {
            Name = new global::app.data.@this<global::app.type.item.variable.@this>("", new global::app.type.item.variable.@this("response")),
            Value = app.actor.list.User.Context.Ok("hello"),
        }, context);

        await app.Run<global::app.module.variable.Set>(new global::app.module.variable.Set(app.actor.list.User.Context)
        {
            Name = new global::app.data.@this<global::app.type.item.variable.@this>("",
                global::app.type.item.variable.@this.Resolve("%response!cost%", context)),
            Value = app.actor.list.User.Context.Ok(100),
        }, context);

        var response = await context.Variable.Get("response");
        await Assert.That((await response.Properties.Value("cost"))).IsEqualTo(100);
    }

    [Test] public async Task VariableSet_BangOnUnsetVariable_IsVariableNotFound()
    {
        await using var app = TestApp.Create("/tmp/var-set-unset-" + System.Guid.NewGuid().ToString("N")[..8]);
        var context = app.actor.list.User.Context;

        var result = await app.Run<global::app.module.variable.Set>(new global::app.module.variable.Set(app.actor.list.User.Context)
        {
            Name = new global::app.data.@this<global::app.type.item.variable.@this>("",
                global::app.type.item.variable.@this.Resolve("%response!cost%", context)),
            Value = app.actor.list.User.Context.Ok(100),
        }, context);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("VariableNotFound");
    }
}
