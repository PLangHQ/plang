namespace PLang.Tests.App.Serialization;

// data-normalize — Stage 2
// IWriter is the format encoder protocol. JsonWriter is the first concrete adapter.
// A future ProtobufWriter / MsgPackWriter / CborWriter ships as a sibling without changes to
// Normalize or any domain type. The minimal surface is documented in stage-2.
// Tests pin (a) the interface surface exists, and (b) JsonWriter emits the right bytes
// per primitive and per structural primitive.

public class IWriterContractTests
{
    // Interface shape -----------------------------------------------------------
    [Test] public async Task IWriter_Interface_Exists_InSerializersNamespace()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task IWriter_HasMethods_Null_Bool_Int_Long_Double_String_DateTime_Decimal_Bytes()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task IWriter_HasMethods_BeginArray_EndArray_BeginRecord_EndRecord()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    // JsonWriter per-method byte output ----------------------------------------
    [Test] public async Task JsonWriter_Null_EmitsNullToken()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task JsonWriter_Bool_EmitsTrueOrFalse()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task JsonWriter_Int_Long_Double_EmitNumericTokens()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task JsonWriter_String_EmitsQuotedString_WithEscapes()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task JsonWriter_DateTime_EmitsIso8601String()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task JsonWriter_Decimal_EmitsNumericToken()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task JsonWriter_Bytes_EmitsBase64String()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task JsonWriter_BeginArray_EndArray_BracketArrayCorrectly()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task JsonWriter_BeginRecord_EndRecord_EmitDataRecordShape()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task JsonWriter_NestedArrayInsideRecord_RoundTrips()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }
}
