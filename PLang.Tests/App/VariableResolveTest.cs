namespace PLang.Tests.App;

public class VariableResolveTest
{
    [Test] public async Task Resolve_Bang_SplitsNameAndProperty()
    {
        var v = global::app.variables.Variable.Resolve("%response!cost%", null!);
        await Assert.That(v.Name).IsEqualTo("response");
        await Assert.That(v.Property).IsEqualTo("cost");
        await Assert.That(v.IsMalformed).IsFalse();
    }

    [Test] public async Task Resolve_NegationPrefix_KeepsBangInName()
    {
        var v = global::app.variables.Variable.Resolve("%!flag%", null!);
        await Assert.That(v.Name).IsEqualTo("!flag");
        await Assert.That(v.Property).IsNull();
        await Assert.That(v.IsMalformed).IsFalse();
    }

    [Test] public async Task Resolve_NegationPlusProperty_FlagsMalformed()
    {
        // Negation prefix + property suffix together (`%!x!cost%`) has no
        // defined semantics — caught at parse time so write paths fail with
        // a typed syntax error instead of VariableNotFound on "!x".
        var v = global::app.variables.Variable.Resolve("%!response!cost%", null!);
        await Assert.That(v.IsMalformed).IsTrue();
        await Assert.That(v.Property).IsNull();
    }

    [Test] public async Task Resolve_DoubleBang_FlagsMalformed_NoSplit()
    {
        var v = global::app.variables.Variable.Resolve("%x!!cost%", null!);
        await Assert.That(v.IsMalformed).IsTrue();
        await Assert.That(v.Property).IsNull();
    }

    [Test] public async Task Resolve_TripleBang_FlagsMalformed_NoSplit()
    {
        var v = global::app.variables.Variable.Resolve("%x!!!cost%", null!);
        await Assert.That(v.IsMalformed).IsTrue();
        await Assert.That(v.Property).IsNull();
    }

    [Test] public async Task Resolve_ChainedBang_FlagsMalformed_NoSplit()
    {
        var v = global::app.variables.Variable.Resolve("%x!a!b%", null!);
        await Assert.That(v.IsMalformed).IsTrue();
        await Assert.That(v.Property).IsNull();
    }

    [Test] public async Task Resolve_EmptyPropertyKey_FlagsMalformed_NoSplit()
    {
        var v = global::app.variables.Variable.Resolve("%x!%", null!);
        await Assert.That(v.IsMalformed).IsTrue();
        await Assert.That(v.Property).IsNull();
    }

    [Test] public async Task Resolve_BangAfterDot_FlagsMalformed_NoSplit()
    {
        var v = global::app.variables.Variable.Resolve("%x.kind!cost%", null!);
        await Assert.That(v.IsMalformed).IsTrue();
        await Assert.That(v.Property).IsNull();
    }

    [Test] public async Task Resolve_PlainPath_KeepsFullBodyOnName()
    {
        // Pre-Stage-4 behaviour preserved: `%x.y%` lands as Name="x.y" with no
        // property suffix. The parser's '!' split is the only thing that
        // shortens Name; '.'/'[' paths ride along on Name verbatim.
        var v = global::app.variables.Variable.Resolve("%planStep.actions%", null!);
        await Assert.That(v.Name).IsEqualTo("planStep.actions");
        await Assert.That(v.Property).IsNull();
        await Assert.That(v.IsMalformed).IsFalse();
    }

    [Test] public async Task VariableSet_BangSyntax_WritesProperty()
    {
        await using var app = new global::app.@this("/tmp/var-set-bang-" + System.Guid.NewGuid().ToString("N")[..8]);
        var ctx = app.User.Context;

        await app.RunAction<global::app.modules.variable.Set>(new global::app.modules.variable.Set
        {
            Name = new global::app.data.@this<global::app.variables.Variable>("", new global::app.variables.Variable("response")),
            Value = global::app.data.@this.Ok("hello"),
        }, ctx);

        await app.RunAction<global::app.modules.variable.Set>(new global::app.modules.variable.Set
        {
            Name = new global::app.data.@this<global::app.variables.Variable>("",
                global::app.variables.Variable.Resolve("%response!cost%", ctx)),
            Value = global::app.data.@this.Ok(100),
        }, ctx);

        var response = ctx.Variables.Get("response");
        await Assert.That(response.Properties["cost"]).IsEqualTo(100);
    }

    [Test] public async Task VariableSet_MalformedBangSyntax_ReturnsTypedError()
    {
        await using var app = new global::app.@this("/tmp/var-set-malformed-" + System.Guid.NewGuid().ToString("N")[..8]);
        var ctx = app.User.Context;

        await app.RunAction<global::app.modules.variable.Set>(new global::app.modules.variable.Set
        {
            Name = new global::app.data.@this<global::app.variables.Variable>("", new global::app.variables.Variable("response")),
            Value = global::app.data.@this.Ok("hello"),
        }, ctx);

        var result = await app.RunAction<global::app.modules.variable.Set>(new global::app.modules.variable.Set
        {
            Name = new global::app.data.@this<global::app.variables.Variable>("",
                global::app.variables.Variable.Resolve("%response!!cost%", ctx)),
            Value = global::app.data.@this.Ok(100),
        }, ctx);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("InvalidVariableReference");
    }
}
