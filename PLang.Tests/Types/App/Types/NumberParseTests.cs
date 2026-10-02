using number = global::app.type.item.number.@this;
using PKind = global::app.type.item.number.NumberKind;

namespace PLang.Tests.App.Types;

// plang-types — Stage 3
// number.Parse / TryParse / Resolve(string, context) — narrowest-fit, context-free.
// "5"→Int; "5.0"→Decimal; "5e0"→Double; "3000000000"→Long. Non-numeric → null/Data.Error.
// Resolve takes context for signature uniformity, NEVER stores it.

public class NumberParseTests
{
    [Test] public async Task Parse_PlainInt_IsInt()
    {
        var n = number.Parse("5");
        await Assert.That(n).IsNotNull();
        await Assert.That(n!.Kind.Name).IsEqualTo("int");
        await Assert.That((int)n).IsEqualTo(5);
    }

    [Test] public async Task Parse_TooBigForInt_PromotesToLong()
    {
        var n = number.Parse("3000000000");
        await Assert.That(n!.Kind.Name).IsEqualTo("long");
        await Assert.That((long)n).IsEqualTo(3000000000L);
    }

    // an integer past long stays an integer, exact — never a decimal or a double
    [Test]
    [Arguments("12345678901234567890123")]
    [Arguments("-12345678901234567890123")]
    [Arguments("9223372036854775808")]
    public async Task Parse_PastLong_IsBigInteger_Exact(string s)
    {
        var n = number.Parse(s);
        await Assert.That(n!.Kind.Name).IsEqualTo("biginteger");
        await Assert.That(n.Clr<System.Numerics.BigInteger>()).IsEqualTo(System.Numerics.BigInteger.Parse(s));
    }

    // a literal past long, as a .pr holds it (a bare json number), reads back exact
    [Test] public async Task APrLiteral_PastLong_ReadsBackExact()
    {
        await using var app = new global::app.@this("/tmp/numparse-" + System.Guid.NewGuid().ToString("N")[..8]).Testing();
        var ctx = app.actor.list.User.Context;
        var big = System.Numerics.BigInteger.Parse("12345678901234567890123");
        var goal = await global::PLang.Tests.Shared.RealGoalLoad.ViaChannel(app, global::PLang.Tests.Shared.Make.Goal(ctx, "G", "/g.goal",
            global::PLang.Tests.Shared.Make.Step("set n", global::PLang.Tests.Shared.Make.Action(ctx, "variable", "set",
                global::PLang.Tests.Shared.Make.Param(ctx, "Name", "n", "variable"), ("Value", (number)big)))));

        await (await goal.Start(ctx)).IsSuccess();

        var n = (number)(await (await ctx.Variable.Get("n")).Value())!;
        await Assert.That(n.Clr<System.Numerics.BigInteger>()).IsEqualTo(big);
    }

    // a json document's integer reads as an integer — past long exact, a small one still a whole number
    [Test] public async Task AJsonInteger_PastLong_ReadsExact()
    {
        await using var app = new global::app.@this("/tmp/numparse-" + System.Guid.NewGuid().ToString("N")[..8]).Testing();
        var ctx = app.actor.list.User.Context;
        var decoded = await app.type.list.Mime("application/json")
            .Decode(System.Text.Encoding.UTF8.GetBytes("{\"n\": 12345678901234567890123, \"small\": 5}"), ctx, "doc");

        var n = (number)(await (await decoded.Get("n")).Value())!;
        var small = (number)(await (await decoded.Get("small")).Value())!;

        await Assert.That(n.Clr<System.Numerics.BigInteger>()).IsEqualTo(System.Numerics.BigInteger.Parse("12345678901234567890123"));
        await Assert.That(small.Cat.ToString()).IsEqualTo("Integer");
    }

    [Test] public async Task Parse_DecimalPoint_IsDouble()
    {
        var n = number.Parse("5.0");
        await Assert.That(n!.Kind.Name).IsEqualTo("double");
    }

    [Test] public async Task Parse_ScientificNotation_IsDouble()
    {
        var n = number.Parse("5e0");
        await Assert.That(n!.Kind.Name).IsEqualTo("double");
    }

    [Test] public async Task Parse_Negative_PreservesSign()
    {
        var n = number.Parse("-42");
        await Assert.That((int)n!).IsEqualTo(-42);
    }

    [Test] public async Task TryParse_NonNumeric_ReturnsFalse_OutputNull()
    {
        var ok = number.TryParse("hello", out var n);
        await Assert.That(ok).IsFalse();
        await Assert.That(n).IsNull();
    }

    [Test] public async Task Resolve_Context_NotStored_OnInstance()
    {
        // Resolve takes context for signature uniformity but never stores it.
        // We verify by reflection that the instance has no Context-related fields
        // populated after a Resolve.
        await using var app = new global::app.@this(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-num-resolve-" + System.Guid.NewGuid().ToString("N")[..8]));
        var n = number.Resolve("3.14", app.actor.list.User.Context);
        var fields = typeof(number).GetFields(System.Reflection.BindingFlags.Instance
                                          | System.Reflection.BindingFlags.NonPublic);
        foreach (var f in fields)
        {
            var v = f.GetValue(n);
            // No field holds the context.
            if (v != null)
                await Assert.That(v is global::app.actor.context.@this).IsFalse();
        }
    }

    [Test] public async Task Resolve_EmptyString_ReturnsNull()
    {
        await Assert.That(number.Parse("")).IsNull();
        await Assert.That(number.Parse("   ")).IsNull();
    }
}
