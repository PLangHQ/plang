namespace PLang.Tests.App.DataTests;

// data-normalize — Stage 2
// Data.Normalize() walks data.Value into a uniform tree of:
//   primitive | byte[] | Data | List<>
// Reflection fires exactly once per type here; format encoders never reflect.
// Normalize is lazy (called by the serializer), idempotent, and bounded.

public class NormalizeTreeShapeTests
{
    [Test] public async Task Normalize_Null_ReturnsNull()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Normalize_String_ReturnsUnchanged()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Normalize_Int_Long_Double_Bool_Decimal_DateTime_ReturnUnchanged()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Normalize_ByteArray_ReturnsUnchanged_OpaqueBinaryLeaf()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Normalize_HomogeneousPrimitiveList_StaysListOfPrimitives()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Normalize_HeterogeneousList_BecomesListOfData()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Normalize_NestedData_RecursesAndStaysData()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Normalize_DictionaryStringX_BecomesListOfData_KeysAsNames()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Normalize_DomainObject_EmitsOneChildPerOutProperty_LowercasedName()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Normalize_RecordType_EmitsOneChildPerOutProperty()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Normalize_IsIdempotent_CallingTwiceProducesSameTree()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Normalize_PropertyLookupCache_PopulatesOnFirstCall_HitsOnSecond()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Normalize_UnsupportedType_ThrowsTypedError()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }
}
