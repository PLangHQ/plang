namespace PLang.Tests.App.Types;

// plang-types — Stage 1
// .pr parameter shape grows an optional `kind` sibling to `type`.
// NEVER a "type:kind" string — splitting a string is runtime work.

public class KindFieldTests
{
    [Test] public async Task PrParameter_HasOptionalKindField_OmittedWhenAbsent()
        => throw new global::System.NotImplementedException();

    [Test] public async Task PrParameter_KindWritten_WhenTypeProducesOne()
        => throw new global::System.NotImplementedException();

    [Test] public async Task PrParameter_KindAndTypeAreSeparateFields_NeverColonString()
        => throw new global::System.NotImplementedException();

    [Test] public async Task PrParameter_KindNull_NotSerializedAsLiteralNull()
        => throw new global::System.NotImplementedException();

    [Test] public async Task PrParameter_RoundTrip_PreservesKindAcrossWriteAndRead()
        => throw new global::System.NotImplementedException();
}
