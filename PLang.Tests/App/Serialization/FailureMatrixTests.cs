namespace PLang.Tests.App.Serialization;

// data-serialize-cleanup — Failure matrix.
// Negative-path tests not absorbed by the per-stage suites above. Each test asserts
// the failure is hard, typed, and surfaces at the right layer.
// Architect ref: .bot/data-serialize-cleanup/architect/plan/test-coverage.md, failure matrix.

public class FailureMatrixTests
{
    // EnsureSigned called on a Data with no Context — InvalidOperationException
    // with a message about Context wiring.
    [Test] public async Task EnsureSigned_OnDataWithoutContext_ThrowsInvalidOperation()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // Properties[key] = value where value is an unsupported type (e.g. Data instance) —
    // ArgumentException "not a wire-supported primitive".
    [Test] public async Task PropertiesSet_DataInstanceValue_ThrowsArgumentException()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    [Test] public async Task PropertiesSet_ArbitraryObjectValue_ThrowsArgumentException()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // signing.verify after wire-byte tampering — Data<bool>.FromError(DataHashMismatch).
    [Test] public async Task SigningVerify_AfterWireByteTamper_ReturnsDataHashMismatch()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // Wire converter Read on random JSON missing the reserved fields —
    // JsonException "Unterminated Data object" or a default-init Data with Error populated.
    [Test] public async Task WireConverter_Read_RandomJsonMissingReservedFields_ProducesTypedFailure()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // Decompress on a Data whose Type is not "archived" — no-op (returns self), NOT an error.
    [Test] public async Task Decompress_OnNonArchivedType_ReturnsSelfNoError()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // Decompress on archived Data whose value is not byte[] —
    // Data.FromError(DecompressError "no byte[] value").
    [Test] public async Task Decompress_OnArchivedWithoutByteArrayValue_ReturnsDataWithDecompressError()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // crypto.Hash with an unsupported algorithm string —
    // Data<byte[]>.FromError(ActionError "UnsupportedAlgorithm").
    [Test] public async Task CryptoHash_WithUnsupportedAlgorithm_ReturnsDataWithUnsupportedAlgorithmError()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // channel.Write on a channel with Direction == Input — Data.FromError(ChannelReadOnly).
    [Test] public async Task ChannelWrite_OnInputOnlyChannel_ReturnsServiceErrorChannelReadOnly()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // channel.Read on a channel with Direction == Output — Data.FromError(ChannelWriteOnly).
    [Test] public async Task ChannelRead_OnOutputOnlyChannel_ReturnsServiceErrorChannelWriteOnly()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // channel.Ask with no interactive answerer (closed pipe) — Data.FromError(ChannelEof).
    [Test] public async Task ChannelAsk_OnClosedPipe_ReturnsServiceErrorChannelEof()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }
}
