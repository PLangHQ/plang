using PNum = global::app.types.number.@this;
using PKind = global::app.types.number.NumberKind;

namespace PLang.Tests.App.Types;

// plang-types — Stage 3
// app/types/number/this.cs — sealed class @this : IEquatable<@this>, IBooleanResolvable.
// Immutable; readonly slots _i / _d / _f; NumberKind { Int, Long, Float, Double, Decimal }.
// int/long/decimal/double/float are KINDS of number — not separate top-level types.
// No Context, no IContext stored.

public class NumberValueTests
{
    [Test] public async Task From_Int_StoresKindInt()
        => await Assert.That(PNum.From(5).Kind).IsEqualTo(PKind.Int);

    [Test] public async Task From_Long_StoresKindLong()
        => await Assert.That(PNum.From(5L).Kind).IsEqualTo(PKind.Long);

    [Test] public async Task From_Decimal_StoresKindDecimal()
        => await Assert.That(PNum.From(5m).Kind).IsEqualTo(PKind.Decimal);

    [Test] public async Task From_Float_StoresKindFloat()
        => await Assert.That(PNum.From(5f).Kind).IsEqualTo(PKind.Float);

    [Test] public async Task From_Double_StoresKindDouble()
        => await Assert.That(PNum.From(5d).Kind).IsEqualTo(PKind.Double);

    [Test]
    public async Task Implicit_InFromConcrete_AllFiveKinds_Compiles()
    {
        PNum a = 5;
        PNum b = 5L;
        PNum c = 5m;
        PNum d = 5f;
        PNum e = 5d;
        await Assert.That(a.Kind).IsEqualTo(PKind.Int);
        await Assert.That(b.Kind).IsEqualTo(PKind.Long);
        await Assert.That(c.Kind).IsEqualTo(PKind.Decimal);
        await Assert.That(d.Kind).IsEqualTo(PKind.Float);
        await Assert.That(e.Kind).IsEqualTo(PKind.Double);
    }

    [Test]
    public async Task Explicit_OutToConcrete_LossyNarrowing_Throws()
    {
        var big = PNum.From(long.MaxValue);
        await Assert.That(() => (int)big).Throws<System.OverflowException>();
    }

    [Test]
    public async Task Explicit_OutToConcrete_InRange_RoundTrips()
    {
        await Assert.That((int)PNum.From(42)).IsEqualTo(42);
        await Assert.That((long)PNum.From(42L)).IsEqualTo(42L);
        await Assert.That((decimal)PNum.From(42m)).IsEqualTo(42m);
        await Assert.That((double)PNum.From(42d)).IsEqualTo(42d);
    }

    [Test]
    public async Task Explicit_IntCast_OnNaN_Throws()
    {
        var nan = PNum.From(double.NaN);
        await Assert.That(() => (int)nan).Throws<System.ArithmeticException>();
    }

    [Test]
    public async Task Immutable_NoPublicSetters_AllSlotsReadonly()
    {
        var setters = typeof(PNum).GetProperties()
            .Where(p => p.CanWrite && p.SetMethod?.IsPublic == true)
            .Select(p => p.Name)
            .ToList();
        await Assert.That(setters).IsEmpty();

        var fields = typeof(PNum).GetFields(System.Reflection.BindingFlags.Instance
                                          | System.Reflection.BindingFlags.NonPublic);
        foreach (var f in fields)
            await Assert.That(f.IsInitOnly).IsTrue();
    }

    [Test]
    public async Task IBooleanResolvable_Zero_IsFalsy()
    {
        await Assert.That(await PNum.From(0).AsBooleanAsync()).IsFalse();
        await Assert.That(await PNum.From(0m).AsBooleanAsync()).IsFalse();
        await Assert.That(await PNum.From(0d).AsBooleanAsync()).IsFalse();
    }

    [Test]
    public async Task IBooleanResolvable_NonZero_IsTruthy()
    {
        await Assert.That(await PNum.From(1).AsBooleanAsync()).IsTrue();
        await Assert.That(await PNum.From(-1).AsBooleanAsync()).IsTrue();
        await Assert.That(await PNum.From(0.1m).AsBooleanAsync()).IsTrue();
    }

    [Test]
    public async Task IBooleanResolvable_NaN_IsFalsy()
        => await Assert.That(await PNum.From(double.NaN).AsBooleanAsync()).IsFalse();

    [Test]
    public async Task NumberDoesNotImplementOrStore_IContextOrContextReference()
    {
        var ctxInterface = typeof(PNum).GetInterfaces()
            .FirstOrDefault(i => i.Name == "IContext");
        await Assert.That(ctxInterface).IsNull();

        var ctxProp = typeof(PNum).GetProperty("Context");
        await Assert.That(ctxProp).IsNull();
    }

    [Test]
    public async Task PlangTypeAttribute_Number_IsRegistered()
    {
        var types = new EngineTypes();
        await Assert.That(types.ResolveType("number")).IsEqualTo(typeof(PNum));
        await Assert.That(types.ResolveName(typeof(PNum))).IsEqualTo("number");
    }
}
