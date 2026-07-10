namespace PLang.Tests.App.Types;

// plang-types — Stage 7 end-to-end runtime DLL roundtrip.
// Loads the TypeProvider.dll fixture (Money + CustomInt + their renderers) and
// drives a real value through the runtime-registered renderer table. This is
// the cross-assembly coverage the Cut4 plang goals can't express (the goal
// language has no surface for constructing arbitrary CLR instances).

public class TypeProviderDllRoundtripTests
{
    private static readonly string FixtureDll = System.IO.Path.GetFullPath(
        System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..",
            "..", "Shared", "Fixtures", "dlls", "TypeProvider.dll"));

    private static System.Reflection.Assembly LoadFixture() => System.Reflection.Assembly.LoadFrom(FixtureDll);

    [Test] public async Task LoadDll_Money_RegistersTypeAndRenderer_ProducesExpectedWireString()
    {
        var asm = LoadFixture();
        var types = new global::app.type.list.@this();
        var result = global::app.type.list.Loader.Register(asm, types);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.RegisteredTypes).Contains("money");

        var moneyType = types.ResolveType("money");
        await Assert.That(moneyType).IsNotNull();
        await Assert.That(moneyType!.FullName).IsEqualTo("TypeProvider.Money");

        var money = System.Activator.CreateInstance(moneyType, 10m, "USD")!;

        var write = types.Renderer.Of("money", "json");
        await Assert.That(write).IsNotNull();

        var captured = new CapturingWriter("json");
        write!(money, captured);
        await Assert.That(captured.Captured).IsEqualTo("USD 10");
    }

    [Test] public async Task LoadDll_CustomInt_OverridesBuiltInName_RuntimeRendererWins()
    {
        var asm = LoadFixture();
        var types = new global::app.type.list.@this();

        // Capture baseline for "int" before the override — int is bootstrap-seeded
        // and may or may not have a generator-emitted renderer; what matters is
        // that after Loader.Register, the runtime entry wins.
        var beforeType = types.ResolveType("int");
        await Assert.That(beforeType).IsEqualTo(typeof(int));

        var result = global::app.type.list.Loader.Register(asm, types);
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.RegisteredTypes).Contains("int");

        // ResolveType — runtime wins over the bootstrap "int" → System.Int32 entry.
        var afterType = types.ResolveType("int");
        await Assert.That(afterType).IsNotNull();
        await Assert.That(afterType!.FullName).IsEqualTo("TypeProvider.CustomInt");

        // Renderer — runtime renderer fires regardless of which value it gets.
        var write = types.Renderer.Of("int", "json");
        await Assert.That(write).IsNotNull();

        var captured = new CapturingWriter("json");
        write!(System.Activator.CreateInstance(afterType)!, captured);
        await Assert.That(captured.Captured).IsEqualTo("CUSTOM-INT");
    }

    private sealed class CapturingWriter : global::app.channel.serializer.IWriter
    {
        public CapturingWriter(string format) { Format = format; }
        public string Format { get; }
        public string? Captured { get; private set; }
        public void Null() { Captured = "(null)"; }
        public void Bool(bool v) { Captured = v.ToString(); }
        public void Int(int v) { Captured = v.ToString(); }
        public void Long(long v) { Captured = v.ToString(); }
        public void Float(float v) { Captured = v.ToString(); }
        public void Double(double v) { Captured = v.ToString(); }
        public void String(string v) { Captured = v; }
        public void DateTime(System.DateTime v) { Captured = v.ToString("O"); }
        public void DateTimeOffset(System.DateTimeOffset v) { Captured = v.ToString("O"); }
        public void TimeSpan(System.TimeSpan v) { Captured = v.ToString(); }
        public void Guid(System.Guid v) { Captured = v.ToString(); }
        public void Enum(System.Enum v) { Captured = v.ToString(); }
        public void Decimal(decimal v) { Captured = v.ToString(); }
        public void Bytes(byte[] v) { Captured = System.Convert.ToBase64String(v); }
        public void BeginArray(int c) { }
        public void BeginObject() { }
        public void Name(string n) { }
        public void EndObject() { }
        public void EndArray() { }
        public void BeginRecord(global::app.data.@this r) { }
        public void EndRecord() { }
        public void Value(object? n) { Captured = n?.ToString(); }
    }
}
