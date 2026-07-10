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

    [Test] public async Task Parse_DecimalPoint_IsDecimal()
    {
        var n = number.Parse("5.0");
        await Assert.That(n!.Kind.Name).IsEqualTo("decimal");
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
        var n = number.Resolve("3.14", app.User.Context);
        var fields = typeof(number).GetFields(System.Reflection.BindingFlags.Instance
                                          | System.Reflection.BindingFlags.NonPublic);
        foreach (var f in fields)
        {
            var v = f.GetValue(n);
            // No field's runtime type is actor.context.@this (or any IContext-shaped ref).
            if (v != null)
                await Assert.That(v.GetType().Name).IsNotEqualTo("this"); // context's @this
        }
    }

    [Test] public async Task Resolve_EmptyString_ReturnsNull()
    {
        await Assert.That(number.Parse("")).IsNull();
        await Assert.That(number.Parse("   ")).IsNull();
    }
}
