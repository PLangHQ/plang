namespace PLang.Tests.App.Types;

// plang-types — Stage 3
// Operators + - * / % == != on number@this are lenient default (policy-free).
// They are the back-of-curtain throwing path; Stage 4's policy-aware methods wrap them.
// (No < > here — comparison ops follow when a comparator handler needs them.)

public class NumberOperatorsTests
{
    [Test] public async Task OperatorAdd_IntInt_ReturnsInt()
        => throw new global::System.NotImplementedException();

    [Test] public async Task OperatorSub_IntInt_ReturnsInt()
        => throw new global::System.NotImplementedException();

    [Test] public async Task OperatorMul_IntInt_ReturnsInt()
        => throw new global::System.NotImplementedException();

    [Test] public async Task OperatorDiv_SevenByTwo_ReturnsThreeAndHalf_LeavesIntegerTrack()
        => throw new global::System.NotImplementedException();

    [Test] public async Task OperatorMod_DefaultLenient_NoPolicyDispatch()
        => throw new global::System.NotImplementedException();

    [Test] public async Task OperatorEq_DelegatesToLenientEquals()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Operators_OnOverflow_Throw_NotData()
        => throw new global::System.NotImplementedException();
}
