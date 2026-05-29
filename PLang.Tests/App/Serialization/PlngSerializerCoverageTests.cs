namespace PLang.Tests.App.Serialization;

// plang-types — Stage 2
// PLNG_SerializerCoverage build gate (error severity): every [PlangType] must have
// a Default.cs serializer OR cover every registered format token. The gate makes the
// runtime writer lookup TOTAL for built-in types — no runtime "unknown format" branch.

public class PlngSerializerCoverageTests
{
    [Test] public async Task Coverage_PlangTypeWithDefaultCs_PassesGate()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Coverage_PlangTypeWithEveryFormatFile_PassesGate_NoDefaultCs()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Coverage_PlangTypeWithNoSerializer_FailsBuild()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Coverage_PlangTypeMissingOneFormat_WithoutDefault_FailsBuild()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Coverage_DiagnosticIdIsPLNG_ErrorSeverity()
        => throw new global::System.NotImplementedException();
}
