using number = global::app.type.number.@this;
using PKind = global::app.type.number.NumberKind;

namespace PLang.Tests.App.Types;

// plang-types — Stage 3
// app/type/number/this.cs — sealed class @this : IEquatable<@this>, IBooleanResolvable.
// Immutable; readonly slots _i / _d / _f; NumberKind { Int, Long, Float, Double, Decimal }.
// int/long/decimal/double/float are KINDS of number — not separate top-level types.
// No Context, no IContext stored.

public class NumberValueTests
{
    [Test] public async Task From_Int_StoresKindInt()
        => await Assert.That(number.From(5).Kind).IsEqualTo(PKind.Int);

    [Test] public async Task From_Long_StoresKindLong()
        => await Assert.That(number.From(5L).Kind).IsEqualTo(PKind.Long);

    [Test] public async Task From_Decimal_StoresKindDecimal()
        => await Assert.That(number.From(5m).Kind).IsEqualTo(PKind.Decimal);

    [Test] public async Task From_Float_StoresKindFloat()
        => await Assert.That(number.From(5f).Kind).IsEqualTo(PKind.Float);

    [Test] public async Task From_Double_StoresKindDouble()
        => await Assert.That(number.From(5d).Kind).IsEqualTo(PKind.Double);

    [Test]
    public async Task Implicit_InFromConcrete_AllFiveKinds_Compiles()
    {
        number a = 5;
        number b = 5L;
        number c = 5m;
        number d = 5f;
        number e = 5d;
        await Assert.That(a.Kind).IsEqualTo(PKind.Int);
        await Assert.That(b.Kind).IsEqualTo(PKind.Long);
        await Assert.That(c.Kind).IsEqualTo(PKind.Decimal);
        await Assert.That(d.Kind).IsEqualTo(PKind.Float);
        await Assert.That(e.Kind).IsEqualTo(PKind.Double);
    }

    [Test]
    public async Task Explicit_OutToConcrete_LossyNarrowing_Throws()
    {
        var big = number.From(long.MaxValue);
        await Assert.That(() => (int)big).Throws<System.OverflowException>();
    }

    [Test]
    public async Task Explicit_OutToConcrete_InRange_RoundTrips()
    {
        await Assert.That((int)number.From(42)).IsEqualTo(42);
        await Assert.That((long)number.From(42L)).IsEqualTo(42L);
        await Assert.That((decimal)number.From(42m)).IsEqualTo(42m);
        await Assert.That((double)number.From(42d)).IsEqualTo(42d);
    }

    [Test]
    public async Task Explicit_IntCast_OnNaN_Throws()
    {
        var nan = number.From(double.NaN);
        await Assert.That(() => (int)nan).Throws<System.ArithmeticException>();
    }

    [Test]
    public async Task Immutable_NoPublicSetters_AllSlotsReadonly()
    {
        var setters = typeof(number).GetProperties()
            .Where(p => p.CanWrite && p.SetMethod?.IsPublic == true)
            // init-only accessors are immutable after creation — same
            // exemption the shared WrapperImmutabilityTests gate applies.
            .Where(p => !p.SetMethod!.ReturnParameter.GetRequiredCustomModifiers()
                .Contains(typeof(System.Runtime.CompilerServices.IsExternalInit)))
            .Select(p => p.Name)
            .ToList();
        await Assert.That(setters).IsEmpty();

        var fields = typeof(number).GetFields(System.Reflection.BindingFlags.Instance
                                          | System.Reflection.BindingFlags.NonPublic);
        foreach (var f in fields)
            await Assert.That(f.IsInitOnly).IsTrue();
    }

    [Test]
    public async Task IBooleanResolvable_Zero_IsFalsy()
    {
        await Assert.That(await number.From(0).AsBooleanAsync()).IsFalse();
        await Assert.That(await number.From(0m).AsBooleanAsync()).IsFalse();
        await Assert.That(await number.From(0d).AsBooleanAsync()).IsFalse();
    }

    [Test]
    public async Task IBooleanResolvable_NonZero_IsTruthy()
    {
        await Assert.That(await number.From(1).AsBooleanAsync()).IsTrue();
        await Assert.That(await number.From(-1).AsBooleanAsync()).IsTrue();
        await Assert.That(await number.From(0.1m).AsBooleanAsync()).IsTrue();
    }

    [Test]
    public async Task IBooleanResolvable_NaN_IsFalsy()
        => await Assert.That(await number.From(double.NaN).AsBooleanAsync()).IsFalse();

    [Test]
    public async Task NumberDoesNotImplementOrStore_IContextOrContextReference()
    {
        var ctxInterface = typeof(number).GetInterfaces()
            .FirstOrDefault(i => i.Name == "IContext");
        await Assert.That(ctxInterface).IsNull();

        var ctxProp = typeof(number).GetProperty("Context");
        await Assert.That(ctxProp).IsNull();
    }

    [Test]
    public async Task PlangTypeAttribute_Number_IsRegistered()
    {
        var types = new global::app.type.catalog.@this();
        await Assert.That(types.ResolveType("number")).IsEqualTo(typeof(number));
        await Assert.That(types.ResolveName(typeof(number))).IsEqualTo("number");
    }
}
