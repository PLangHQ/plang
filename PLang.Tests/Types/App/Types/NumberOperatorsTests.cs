using number = global::app.type.item.number.@this;
using PKind = global::app.type.item.number.NumberKind;

namespace PLang.Tests.App.Types;

// plang-types — Stage 3
// Operators + - * / % == != on number@this are lenient default (policy-free).
// They are the back-of-curtain throwing path; Stage 4's policy-aware methods wrap them.
// (No < > here — comparison ops follow when a comparator handler needs them.)

public class NumberOperatorsTests
{
    [Test] public async Task OperatorAdd_IntInt_ReturnsInt()
    {
        var r = ((number)(2)) + ((number)(3));
        await Assert.That(r.Kind.Name).IsEqualTo("int");
        await Assert.That((int)r).IsEqualTo(5);
    }

    [Test] public async Task OperatorSub_IntInt_ReturnsInt()
    {
        var r = ((number)(7)) - ((number)(2));
        await Assert.That(r.Kind.Name).IsEqualTo("int");
        await Assert.That((int)r).IsEqualTo(5);
    }

    [Test] public async Task OperatorMul_IntInt_ReturnsInt()
    {
        var r = ((number)(3)) * ((number)(4));
        await Assert.That(r.Kind.Name).IsEqualTo("int");
        await Assert.That((int)r).IsEqualTo(12);
    }

    [Test] public async Task OperatorDiv_SevenByTwo_ReturnsThreeAndHalf_LeavesIntegerTrack()
    {
        var r = ((number)(7)) / ((number)(2));
        await Assert.That(r.Kind.Name).IsEqualTo("decimal");
        await Assert.That((decimal)r).IsEqualTo(3.5m);
    }

    [Test] public async Task OperatorMod_DefaultLenient_NoPolicyDispatch()
    {
        var r = ((number)(7)) % ((number)(3));
        await Assert.That(r.Kind.Name).IsEqualTo("int");
        await Assert.That((int)r).IsEqualTo(1);
    }

    [Test] public async Task OperatorEq_DelegatesToLenientEquals()
    {
        await Assert.That(((number)(5)) == ((number)(5L))).IsTrue();
        await Assert.That(((number)(5)) != ((number)(6))).IsTrue();
    }

    [Test] public async Task Operators_OnOverflow_Throw_NotData()
    {
        // C# operators throw at the boundary — Stage 4's policy-aware named
        // methods are what wrap and return Data. Decimal has no wider integer
        // kind to promote into, so overflow surfaces as a throw.
        await Assert.That(() => { var _ = ((number)(decimal.MaxValue)) + ((number)(decimal.MaxValue)); })
            .Throws<System.OverflowException>();
    }
}
