namespace PLang.Tests.App.DataTests;

// data-normalize — Stage 3
// As<T> becomes a tree-walker over the normalized tree, NOT a delegating call to
// JsonSerializer.Deserialize<T>. Each case walks recursively, dispatching per child Data.
//
// Cases:
//   - primitive       → unbox+convert
//   - List<X>         → walk List<Data> or List<X>, As<X> per element
//   - Dictionary<K,V> → name → key, As<V> per value
//   - record / class with [Out] properties → instantiate, populate per named child
//   - type with reconstruction hook → delegate (see AsReconstructionHookTests)

public class AsTreeWalkerTests
{
    [Test] public async Task As_Int_OnDataInt_ReturnsValue()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task As_String_OnDataString_ReturnsValue()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task As_ListInt_OnDataListInt_ReturnsList()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task As_DictionaryStringInt_FromNamedDataChildren_ReturnsMap()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task As_Identity_ReconstructsName_PublicKey_PrivateKeyIsNull()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task As_Identity_IsDefault_IsArchived_Created_TakeDefaults()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task As_RecordWithPositionalCtor_ReconstructsThroughCtor()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task As_NoOutProperties_ReturnsAllDefaultInstance()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task As_T_UsesPropertyLookupCache_OnSecondCallSameType()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    // Negative paths (echoed in FailureMatrixNormalizeTests) -------------------
    [Test] public async Task As_FilePath_WithHttpSchemeInTree_RaisesSchemeMismatch()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task As_RequiredPropertyMissing_RaisesMissingRequiredPropertyError()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task As_TypeMismatch_StringTreeIntoIntProperty_RaisesTypedConversionError()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task As_T_OnTypeWithNoParameterlessCtor_AndNoHook_RaisesNoReconstructionStrategy()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }
}
