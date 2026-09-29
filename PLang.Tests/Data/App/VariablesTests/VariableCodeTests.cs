using Parser = global::app.type.item.variable.parser.@this;
using Variable = global::app.type.item.variable.@this;
using Code = global::app.type.item.variable.code;

namespace PLang.Tests.App.VariablesTests;

// The parser is the one definition of a reference; a variable's code runs its hops in order and
// writes at its last.
public class VariableCodeTests
{
    private global::app.@this _app = null!;
    private global::app.actor.context.@this Context => _app.actor.list.User.Context;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/test").Testing();

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    private static Variable One(string text) => new Parser(text).Read(0)!;

    private static string[] Hops(Variable v) => v.Code.Items().Select(h => $"{h.Kind}:{h.Text}").ToArray();

    private static global::app.type.item.list.@this List(params string[] values)
        => new(values.Select(v => (global::app.type.item.@this)new global::app.type.item.text.@this(v)));

    // ---- a write through a node that holds nothing ----

    // %!app.user…% after the app stopped holding its actors: the write has nowhere to land — an error
    // naming the node that holds nothing, never a quiet success.
    [Test]
    public async Task Set_ThroughANodeThatHoldsNothing_IsAnError_NamingIt()
    {
        var written = await One("%!app.user.callstack.setting.timing%").Set(Context.Ok(true), Context);

        await written.IsFailure();
        await Assert.That(written.Error!.Key).IsEqualTo("VariableNotFound");
        await Assert.That(written.Error.Message).Contains("%!app.user%");
    }

    // A read of a node that holds nothing is "not set" — as any unset variable reads (`is null` is true).
    [Test]
    public async Task Read_ThroughANodeThatHoldsNothing_IsNotSet()
    {
        var read = await One("%!app.user.callstack%").Start(Context);

        await Assert.That(read.IsInitialized).IsFalse();
        await read.IsSuccess();
    }

    [Test]
    public async Task Set_ThroughAMissingKey_IsAnError_NamingIt()
    {
        await Context.Variable.Set("user", new Dictionary<string, object?> { ["name"] = "a" });

        var written = await One("%user.address.city%").Set(Context.Ok("Reykjavik"), Context);

        await written.IsFailure();
        await Assert.That(written.Error!.Message).Contains("%user.address%");
    }

    // ---- the parser ----

    [Test]
    public async Task Parser_FindsEachReferenceInATemplate()
    {
        var found = new Parser("hi %name%, from %user.address[idx].city% (%order!cost%)").Variable;

        await Assert.That(found.Select(v => v.Text).ToArray())
            .IsEquivalentTo(new[] { "%name%", "%user.address[idx].city%", "%order!cost%" });
    }

    [Test]
    public async Task Parser_ANestedIndexVariable_IsOneReference()
    {
        var v = One("%user.address[%i%].city%");

        await Assert.That(Hops(v)).IsEquivalentTo(new[]
            { "variable:user", "property:.address", "index:[%i%]", "property:.city" });
        await Assert.That(((Variable)((Code.Index)v.Code[2]).Key).Text).IsEqualTo("%i%");
    }

    [Test]
    public async Task Parser_ABareIndexPath_IsAVariableKey()
    {
        var v = One("%gui[askInfo.gui]%");

        var key = (Variable)((Code.Index)v.Code[1]).Key;
        await Assert.That(key.Text).IsEqualTo("%askInfo.gui%");
    }

    [Test]
    public async Task Parser_LiteralIndexes_AreANumberAndAText()
    {
        var v = One("%x[0][\"k\"]%");

        await Assert.That(((Code.Index)v.Code[1]).Key).IsTypeOf<global::app.type.item.number.@this>();
        await Assert.That(((Code.Index)v.Code[2]).Key.ToString()).IsEqualTo("k");
    }

    [Test]
    public async Task Parser_AMethodsValues_MayHoldSpacesAndPercent()
    {
        var found = new Parser("%name.replace(\"-\", \" \")% and %x.replace(\"%\", \"\")%").Variable;

        await Assert.That(found.Select(v => v.Text).ToArray())
            .IsEquivalentTo(new[] { "%name.replace(\"-\", \" \")%", "%x.replace(\"%\", \"\")%" });
        var method = (Code.Method)found[0].Code[1];
        await Assert.That(method.Name).IsEqualTo("replace");
        await Assert.That(method.Parameter.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Parser_AQuotedMember_IsItsLiteralKey()
    {
        var v = One("%tags.\"a.b\"%");

        await Assert.That(((Code.Property)v.Code[1]).Name).IsEqualTo("a.b");
    }

    [Test]
    public async Task Parser_APercentThatOpensNothing_IsText()
    {
        await Assert.That(new Parser("50% of %total%").Variable.Select(v => v.Text).ToArray())
            .IsEquivalentTo(new[] { "%total%" });
        await Assert.That(new Parser("100% and 5%").Variable.Count).IsEqualTo(0);
        await Assert.That(new Parser("%% or %x").Variable.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Parser_AReferenceThatDoesntParse_IsAnError()
    {
        var parser = new Parser("hi %x!!y% there");

        await Assert.That(parser.Variable.Count).IsEqualTo(0);
        await Assert.That(parser.Error.Count).IsEqualTo(1);
        await Assert.That(parser.Error[0].Key).IsEqualTo("InvalidVariable");
    }

    [Test]
    public async Task Parser_ReadsTheReferenceAtAPosition()
    {
        var v = new Parser("Name=%x.y% rest").Read(5);

        await Assert.That(v!.Text).IsEqualTo("%x.y%");
    }

    // ---- the code ----

    [Test]
    public async Task Set_AMemberOnANewVariable_MakesItADict()
    {
        await One("%user.name%").Set("Ann", Context);

        var name = await One("%user.name%").Start(Context);
        await Assert.That((await name.Value()).ToString()).IsEqualTo("Ann");
        await Assert.That((await Context.Variable.Get("user")).Peek()).IsTypeOf<global::app.type.item.dict.@this>();
    }

    [Test]
    public async Task Start_ALiteralIndex_IsThatPosition()
    {
        await Context.Variable.Set("items", List("a", "b"));

        var second = await One("%items[1]%").Start(Context);

        await Assert.That((await second.Value()).ToString()).IsEqualTo("b");
    }

    [Test]
    public async Task Start_AVariableIndex_IsWhatTheVariableHolds()
    {
        await Context.Variable.Set("items", List("a", "b"));
        await Context.Variable.Set("idx", 1);

        await Assert.That((await (await One("%items[idx]%").Start(Context)).Value()).ToString()).IsEqualTo("b");
        await Assert.That((await (await One("%items[%idx%]%").Start(Context)).Value()).ToString()).IsEqualTo("b");
    }

    [Test]
    public async Task Start_AnUnsetIndexVariable_IsIndexNotSet()
    {
        await Context.Variable.Set("items", List("a", "b"));

        var missing = await One("%items[nothere]%").Start(Context);

        await missing.IsFailure();
        await Assert.That(missing.Error!.Key).IsEqualTo("IndexNotSet");
    }

    [Test]
    public async Task Set_AnIndex_WritesThatPosition()
    {
        await Context.Variable.Set("items", List("a", "b"));

        await One("%items[0]%").Set("z", Context);

        await Assert.That((await (await One("%items[0]%").Start(Context)).Value()).ToString()).IsEqualTo("z");
    }

    [Test]
    public async Task SetAndStart_ABindingProperty_UseTheProperties()
    {
        await Context.Variable.Set("x", "v");

        await One("%x!cost%").Set(5, Context);

        await Assert.That((await (await One("%x!cost%").Start(Context)).Value()).ToString()).IsEqualTo("5");
    }

    [Test]
    public async Task Start_ABindingName_ReadsDatasOwnMember()
    {
        await Context.Variable.Set("x", "v");

        var name = await One("%x!name%").Start(Context);

        await Assert.That((await name.Value()).ToString()).IsEqualTo("x");
    }

    [Test]
    public async Task Set_AMethod_IsNotWritable()
    {
        await Context.Variable.Set("x", "v");

        var result = await One("%x.trim()%").Set("y", Context);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("VariableNotWritable");
    }

    // ---- text's own methods ----

    private async Task<string> Read(string text) => (await (await One(text).Start(Context)).Value()).ToString()!;

    [Test]
    public async Task Method_TextsOwnMethods_AnswerText()
    {
        await Context.Variable.Set("name", "  a-b-c  ");

        await Assert.That(await Read("%name.trim()%")).IsEqualTo("a-b-c");
        await Assert.That(await Read("%name.trim().replace(\"-\", \" \")%")).IsEqualTo("a b c");
        await Assert.That(await Read("%name.trim().toupper()%")).IsEqualTo("A-B-C");
        await Assert.That(await Read("%name.trim().ToLower()%")).IsEqualTo("a-b-c");
        await Assert.That(await Read("%name.trim().maxlength(3)%")).IsEqualTo("a-b...");
        await Assert.That(await Read("%name.maxlength(0)%")).IsEqualTo("  a-b-c  ");
    }

    [Test]
    public async Task Method_AVariableParameter_IsWhatItHolds()
    {
        await Context.Variable.Set("name", "a-b");
        await Context.Variable.Set("dash", "-");

        await Assert.That(await Read("%name.replace(%dash%, \"+\")%")).IsEqualTo("a+b");
    }

    [Test]
    public async Task Method_Grep_KeepsTheMatchingLines()
    {
        await Context.Variable.Set("log", "ok\nerror one\nok\nerror two");

        await Assert.That(await Read("%log.grepcount(\"error\")%")).IsEqualTo("2");
        await Assert.That(await Read("%log.grep(\"two\")%")).Contains("error two");
    }

    [Test]
    public async Task Method_ThatTheValueDoesntHave_IsAnError()
    {
        await Context.Variable.Set("name", "a");

        var result = await One("%name.foo()%").Start(Context);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("MethodNotFound");
        await Assert.That(result.Error.Message).Contains("text has no method 'foo'");
    }

    [Test]
    public async Task Set_ABareRoot_Rebinds()
    {
        await One("%x%").Set("first", Context);
        await One("%x%").Set("second", Context);

        await Assert.That((await (await Context.Variable.Get("x")).Value()).ToString()).IsEqualTo("second");
    }
}
