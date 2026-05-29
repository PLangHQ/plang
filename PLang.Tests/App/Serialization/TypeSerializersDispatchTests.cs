namespace PLang.Tests.App.Serialization;

// plang-types — Stage 2
// app.type.renderer.@this — the (typeName, formatToken) → Write dispatch table.
// Reflection-discovered from app/type/<name>/serializer/<format>.cs at first use,
// with a Register(...) seam for runtime-loaded DLLs (Stage 7).
// Lookup: specific (type, format) hit → fallback (type, "*") → null.

public class TypeSerializersDispatchTests
{
    private global::app.type.renderer.@this _r = null!;

    [Before(Test)]
    public void Setup() => _r = new global::app.type.renderer.@this();

    [Test]
    public async Task Lookup_SpecificTypeFormat_HitsRegisteredWriter()
    {
        bool called = false;
        _r.Register("dispatch-fixture", "json", (v, w) => called = true);
        var write = _r.Of("dispatch-fixture", "json");
        await Assert.That(write).IsNotNull();
        write!(new object(), new FakeWriter("json"));
        await Assert.That(called).IsTrue();
    }

    [Test]
    public async Task Lookup_NoSpecific_FallsBackToStarDefault()
    {
        bool called = false;
        _r.Register("dispatch-fixture", global::app.type.renderer.@this.AnyFormat, (v, w) => called = true);
        var write = _r.Of("dispatch-fixture", "anything");
        await Assert.That(write).IsNotNull();
        write!(new object(), new FakeWriter("anything"));
        await Assert.That(called).IsTrue();
    }

    [Test]
    public async Task Lookup_UnknownTypeName_ReturnsNullForCaller()
    {
        await Assert.That(_r.Of("never-registered", "json")).IsNull();
        await Assert.That(_r.Has("never-registered")).IsFalse();
    }

    [Test]
    public async Task RegisterRuntime_AddsEntry_LookupSucceedsAfter()
    {
        await Assert.That(_r.Has("late-fixture")).IsFalse();
        _r.Register("late-fixture", global::app.type.renderer.@this.AnyFormat, (v, w) => { });
        await Assert.That(_r.Has("late-fixture")).IsTrue();
        await Assert.That(_r.Of("late-fixture", "json")).IsNotNull();
    }

    [Test]
    public async Task RegisterRuntime_OverrideBuiltIn_RuntimeWins()
    {
        // path comes from the discovered Default. A runtime Register for
        // (path, "*") must shadow it — Stage 7's overwrite story depends on
        // this precedence.
        bool runtimeFired = false;
        _r.Register("path", global::app.type.renderer.@this.AnyFormat, (v, w) => runtimeFired = true);
        var write = _r.Of("path", "json");
        await Assert.That(write).IsNotNull();
        write!(new object(), new FakeWriter("json"));
        await Assert.That(runtimeFired).IsTrue();
    }

    [Test]
    public async Task GeneratorEmits_OneEntryPerSerializerFile_UnderAppTypes()
    {
        // path/serializer/Default.cs ships in the App assembly. After init,
        // the dispatch table must surface it under (path, "*").
        await Assert.That(_r.Has("path")).IsTrue();
        await Assert.That(_r.Of("path", "json")).IsNotNull();
        await Assert.That(_r.Of("path", "plang")).IsNotNull();
    }

    private sealed class FakeWriter : global::app.channel.serializer.IWriter
    {
        public FakeWriter(string format) { Format = format; }
        public string Format { get; }
        public void Null() { }
        public void Bool(bool value) { }
        public void Int(int value) { }
        public void Long(long value) { }
        public void Float(float value) { }
        public void Double(double value) { }
        public void String(string value) { }
        public void DateTime(System.DateTime value) { }
        public void DateTimeOffset(System.DateTimeOffset value) { }
        public void TimeSpan(System.TimeSpan value) { }
        public void Guid(System.Guid value) { }
        public void Enum(System.Enum value) { }
        public void Decimal(decimal value) { }
        public void Bytes(byte[] value) { }
        public void BeginArray(int count) { }
        public void EndArray() { }
        public void BeginRecord(global::app.data.@this record) { }
        public void EndRecord() { }
        public void Value(object? normalized) { }
    }
}
