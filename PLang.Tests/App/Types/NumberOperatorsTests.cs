using number = global::app.type.number.@this;
using PKind = global::app.type.number.NumberKind;

namespace PLang.Tests.App.Types;

// plang-types — Stage 3
// Operators + - * / % == != on number@this are lenient default (policy-free).
// They are the back-of-curtain throwing path; Stage 4's policy-aware methods wrap them.
// (No < > here — comparison ops follow when a comparator handler needs them.)

public class NumberOperatorsTests
{
    [Test] public async Task OperatorAdd_IntInt_ReturnsInt()
    {
        var r = number.From(2) + number.From(3);
        await Assert.That(r.Kind).IsEqualTo(PKind.Int);
        await Assert.That((int)r).IsEqualTo(5);
    }

    [Test] public async Task OperatorSub_IntInt_ReturnsInt()
    {
        var r = number.From(7) - number.From(2);
        await Assert.That(r.Kind).IsEqualTo(PKind.Int);
        await Assert.That((int)r).IsEqualTo(5);
    }

    [Test] public async Task OperatorMul_IntInt_ReturnsInt()
    {
        var r = number.From(3) * number.From(4);
        await Assert.That(r.Kind).IsEqualTo(PKind.Int);
        await Assert.That((int)r).IsEqualTo(12);
    }

    [Test] public async Task OperatorDiv_SevenByTwo_ReturnsThreeAndHalf_LeavesIntegerTrack()
    {
        var r = number.From(7) / number.From(2);
        await Assert.That(r.Kind).IsEqualTo(PKind.Decimal);
        await Assert.That((decimal)r).IsEqualTo(3.5m);
    }

    [Test] public async Task OperatorMod_DefaultLenient_NoPolicyDispatch()
    {
        var r = number.From(7) % number.From(3);
        await Assert.That(r.Kind).IsEqualTo(PKind.Int);
        await Assert.That((int)r).IsEqualTo(1);
    }

    [Test] public async Task OperatorEq_DelegatesToLenientEquals()
    {
        await Assert.That(number.From(5) == number.From(5L)).IsTrue();
        await Assert.That(number.From(5) != number.From(6)).IsTrue();
    }

    [Test] public async Task Operators_OnOverflow_Throw_NotData()
    {
        // C# operators throw at the boundary — Stage 4's policy-aware named
        // methods are what wrap and return Data. Decimal has no wider integer
        // kind to promote into, so overflow surfaces as a throw.
        await Assert.That(() => { var _ = number.From(decimal.MaxValue) + number.From(decimal.MaxValue); })
            .Throws<System.OverflowException>();
    }
}
