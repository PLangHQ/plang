namespace PLang.Tests.Shared;

/// <summary>
/// A live call frame for tests that drive a modifier directly. A modifier runs inside the frame its
/// action pushed — that is where its verdict is recorded — so a test that calls <c>Wrap</c> without
/// an action has to supply the frame the contract assumes.
/// </summary>
public static class TestFrame
{
    public static global::app.callstack.call.@this Live(global::app.actor.context.@this context)
        => context.CallStack.Push(
            TestAction.Create("variable", "set", ("name", "%frame%"), ("value", "1")),
            context.Variable);
}
