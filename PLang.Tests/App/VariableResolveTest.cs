namespace PLang.Tests.App;

public class VariableResolveTest
{
    [Test] public async Task Resolve_Bang_SplitsNameAndProperty()
    {
        var v = global::app.variable.@this.Resolve("%response!cost%", null!);
        await Assert.That(v.Name).IsEqualTo("response");
        await Assert.That(v.Property).IsEqualTo("cost");
        await Assert.That(v.IsMalformed).IsFalse();
    }

    [Test] public async Task Resolve_NegationPrefix_KeepsBangInName()
    {
        var v = global::app.variable.@this.Resolve("%!flag%", null!);
        await Assert.That(v.Name).IsEqualTo("!flag");
        await Assert.That(v.Property).IsNull();
        await Assert.That(v.IsMalformed).IsFalse();
    }

    [Test] public async Task Resolve_NegationPlusProperty_FlagsMalformed()
    {
        // Negation prefix + property suffix together (`%!x!cost%`) has no
        // defined semantics — caught at parse time so write paths fail with
        // a typed syntax error instead of VariableNotFound on "!x".
        var v = global::app.variable.@this.Resolve("%!response!cost%", null!);
        await Assert.That(v.IsMalformed).IsTrue();
        await Assert.That(v.Property).IsNull();
    }

    [Test] public async Task Resolve_DoubleBang_FlagsMalformed_NoSplit()
    {
        var v = global::app.variable.@this.Resolve("%x!!cost%", null!);
        await Assert.That(v.IsMalformed).IsTrue();
        await Assert.That(v.Property).IsNull();
    }

    [Test] public async Task Resolve_TripleBang_FlagsMalformed_NoSplit()
    {
        var v = global::app.variable.@this.Resolve("%x!!!cost%", null!);
        await Assert.That(v.IsMalformed).IsTrue();
        await Assert.That(v.Property).IsNull();
    }

    [Test] public async Task Resolve_ChainedBang_FlagsMalformed_NoSplit()
    {
        var v = global::app.variable.@this.Resolve("%x!a!b%", null!);
        await Assert.That(v.IsMalformed).IsTrue();
        await Assert.That(v.Property).IsNull();
    }

    [Test] public async Task Resolve_EmptyPropertyKey_FlagsMalformed_NoSplit()
    {
        var v = global::app.variable.@this.Resolve("%x!%", null!);
        await Assert.That(v.IsMalformed).IsTrue();
        await Assert.That(v.Property).IsNull();
    }

    [Test] public async Task Resolve_BangAfterDot_FlagsMalformed_NoSplit()
    {
        var v = global::app.variable.@this.Resolve("%x.kind!cost%", null!);
        await Assert.That(v.IsMalformed).IsTrue();
        await Assert.That(v.Property).IsNull();
    }

    [Test] public async Task Resolve_PlainPath_KeepsFullBodyOnName()
    {
        // Pre-Stage-4 behaviour preserved: `%x.y%` lands as Name="x.y" with no
        // property suffix. The parser's '!' split is the only thing that
        // shortens Name; '.'/'[' paths ride along on Name verbatim.
        var v = global::app.variable.@this.Resolve("%planStep.actions%", null!);
        await Assert.That(v.Name).IsEqualTo("planStep.actions");
        await Assert.That(v.Property).IsNull();
        await Assert.That(v.IsMalformed).IsFalse();
    }

    [Test] public async Task VariableSet_BangSyntax_WritesProperty()
    {
        await using var app = new global::app.@this("/tmp/var-set-bang-" + System.Guid.NewGuid().ToString("N")[..8]);
        var context = app.User.Context;

        await app.RunAction<global::app.module.variable.Set>(new global::app.module.variable.Set
        {
            Name = new global::app.data.@this<global::app.variable.@this>("", new global::app.variable.@this("response")),
            Value = global::app.data.@this.Ok("hello"),
        }, context);

        await app.RunAction<global::app.module.variable.Set>(new global::app.module.variable.Set
        {
            Name = new global::app.data.@this<global::app.variable.@this>("",
                global::app.variable.@this.Resolve("%response!cost%", context)),
            Value = global::app.data.@this.Ok(100),
        }, context);

        var response = context.Variable.Get("response");
        await Assert.That(response.Properties["cost"]).IsEqualTo(100);
    }

    [Test] public async Task VariableSet_MalformedBangSyntax_ReturnsTypedError()
    {
        await using var app = new global::app.@this("/tmp/var-set-malformed-" + System.Guid.NewGuid().ToString("N")[..8]);
        var context = app.User.Context;

        await app.RunAction<global::app.module.variable.Set>(new global::app.module.variable.Set
        {
            Name = new global::app.data.@this<global::app.variable.@this>("", new global::app.variable.@this("response")),
            Value = global::app.data.@this.Ok("hello"),
        }, context);

        var result = await app.RunAction<global::app.module.variable.Set>(new global::app.module.variable.Set
        {
            Name = new global::app.data.@this<global::app.variable.@this>("",
                global::app.variable.@this.Resolve("%response!!cost%", context)),
            Value = global::app.data.@this.Ok(100),
        }, context);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("InvalidVariableReference");
    }
}
