namespace PLang.Tests.App.Types;

// plang-types — Stage 7
// `- load X.dll` scans the assembly for [PlangType] classes → Registry.RegisterRuntime,
// and for ITypeRenderer impls → renderer.@this.Register. Runtime registrations
// outrank built-ins (ResolveType + Renderers.Of precedence). A loaded [PlangType]
// with no covering renderer fails the load.
//
// Tests use the in-test fixture assembly via app.type.list.Loader (the static helper
// behind code.load) — no real DLL roundtrip needed to verify the wiring.

public class RuntimeTypeLoadingTests
{
    [global::app.Attributes.PlangType("runtime-fixture-only")]
    public sealed class FixtureOnly
    {
        public static string Example => "x";
        public static string Shape => "string";
    }

    public sealed class FixtureOnlyRenderer : global::app.type.list.ITypeRenderer
    {
        public string TypeName => "runtime-fixture-only";
        public string Format => global::app.type.renderer.@this.AnyFormat;
        public void Write(object value, global::app.channel.serializer.IWriter writer)
            => writer.String("[runtime-fixture-only]");
    }

    [global::app.Attributes.PlangType("runtime-fixture-norenderer")]
    public sealed class FixtureNoRenderer
    {
        public static string Shape => "string";
    }

    public sealed class OverrideIntRenderer : global::app.type.list.ITypeRenderer
    {
        public string TypeName => "int";
        public string Format => global::app.type.renderer.@this.AnyFormat;
        public void Write(object value, global::app.channel.serializer.IWriter writer)
            => writer.String("OVERRIDDEN");
    }

    // We isolate each scan to a freshly built fixture assembly so the runtime
    // overrides don't leak between tests. Use the test assembly directly —
    // its [PlangType] fixtures live in this file.
    private static System.Reflection.Assembly TestAssembly =>
        typeof(RuntimeTypeLoadingTests).Assembly;

    [Test] public async Task LoadDll_PlangTypeClass_RegistersViaRegistryRegisterRuntime()
    {
        var types = new global::app.type.list.@this();
        var result = global::app.type.list.Loader.Register(TestAssembly, types);
        // The fixture types in this file should appear in RegisteredTypes.
        await Assert.That(result.RegisteredTypes).Contains("runtime-fixture-only");
        await Assert.That(types.ResolveType("runtime-fixture-only")).IsEqualTo(typeof(FixtureOnly));
    }

    [Test] public async Task LoadDll_ITypeRenderer_RegistersIntoTypeSerializers()
    {
        var types = new global::app.type.list.@this();
        var result = global::app.type.list.Loader.Register(TestAssembly, types);
        await Assert.That(types.Renderers.Has("runtime-fixture-only")).IsTrue();
        await Assert.That(types.Renderers.Of("runtime-fixture-only", "json")).IsNotNull();
    }

    [Test] public async Task LoadDll_ExistingName_RuntimeWinsAtResolveType()
    {
        // Register a runtime entry under "int" — runtime should shadow the
        // bootstrap-seeded primitive.
        var types = new global::app.type.list.@this();
        types.Register("int", typeof(System.Uri));
        await Assert.That(types.ResolveType("int")).IsEqualTo(typeof(System.Uri));
    }

    [Test] public async Task LoadDll_ExistingName_RuntimeRendererWinsAtTypeSerializersLookup()
    {
        // "path" has a generator-emitted Default.cs renderer; register a runtime
        // one over it and assert the runtime delegate wins. Captures both
        // outputs so a regression that resolved generated first would fail.
        var types = new global::app.type.list.@this();
        var generatedWriter = new FakeWriter("json");
        var baseline = types.Renderers.Of("path", "json");
        await Assert.That(baseline).IsNotNull();

        string fired = "none";
        types.Renderers.Register("path", "json",
            (v, w) => fired = "runtime");

        var after = types.Renderers.Of("path", "json");
        await Assert.That(after).IsNotNull();
        await Assert.That(System.Object.ReferenceEquals(after, baseline)).IsFalse();

        after!("ignored", generatedWriter);
        await Assert.That(fired).IsEqualTo("runtime");
    }

    [Test] public async Task LoadDll_PlangTypeWithoutAnyRenderer_FailsLoad_TypedError()
    {
        // Loader registers all [PlangType] classes in pass 1, then enforces the
        // coverage gate. The fixture-norenderer type must register, and the
        // gate must surface a TypeLoadCoverage failure for it.
        var result = global::app.type.list.Loader.Register(TestAssembly, new global::app.type.list.@this());
        await Assert.That(result.RegisteredTypes).Contains("runtime-fixture-norenderer");
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.ErrorKey).IsEqualTo("TypeLoadCoverage");
    }

    [Test] public async Task LoadDll_AlreadyCompiledHandlerSlot_StillSeesBuiltInType_NoRewrite()
    {
        // Honest limit: runtime registration changes resolution + rendering,
        // not the compiled handler IL. math.Add.Run returns Task<Data<number>>
        // baked in by the source generator at compile time; assert the generic
        // argument stays the built-in number type before AND after overriding
        // "number" in the runtime registry.
        var runReturn = typeof(global::app.module.math.Add).GetMethod("Run")!.ReturnType;
        var dataGeneric = runReturn.GetGenericArguments()[0];
        await Assert.That(dataGeneric.IsGenericType).IsTrue();
        await Assert.That(dataGeneric.GetGenericTypeDefinition()).IsEqualTo(typeof(global::app.data.@this<>));
        var slotBefore = dataGeneric.GetGenericArguments()[0];
        await Assert.That(slotBefore).IsEqualTo(typeof(global::app.type.item.number.@this));

        var types = new global::app.type.list.@this();
        types.Register("number", typeof(System.Uri));
        await Assert.That(types.ResolveType("number")).IsEqualTo(typeof(System.Uri));

        var slotAfter = typeof(global::app.module.math.Add).GetMethod("Run")!
            .ReturnType.GetGenericArguments()[0].GetGenericArguments()[0];
        await Assert.That(slotAfter).IsEqualTo(typeof(global::app.type.item.number.@this));
    }

    private static string FixtureDll(string name) => System.IO.Path.GetFullPath(
        System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..",
            "..", "Shared", "Fixtures", "dlls", name));

    private static readonly string IdentityShadowDll = FixtureDll("IdentityShadow.dll");
    private static readonly string SignatureRendererShadowDll = FixtureDll("SignatureRendererShadow.dll");
    private static readonly string CallbackInferredShadowDll = FixtureDll("CallbackInferredShadow.dll");

    [Test] public async Task LoadDll_AttemptToShadowSealedName_FailsWith_TypeLoadCollision()
    {
        // Pass-1 explicit [PlangType("identity")] — sealed-name gate refuses
        // with TypeLoadCollision before the registry sees the type. Replacing
        // identity's CLR type would let a runtime DLL compose the body that
        // gets signed under the actor's key.
        var asm = System.Reflection.Assembly.LoadFrom(IdentityShadowDll);
        var result = global::app.type.list.Loader.Register(asm, new global::app.type.list.@this());
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.ErrorKey).IsEqualTo("TypeLoadCollision");
        await Assert.That(result.ErrorMessage).Contains("identity");
    }

    [Test] public async Task LoadDll_SealedNameAsRendererTypeName_FailsWith_TypeLoadCollision()
    {
        // Pass-2 ITypeRenderer registration — a renderer whose TypeName is
        // sealed ("signature") is refused before it can replace the wire
        // shape of an existing built-in. This is the renderer-substitution
        // attack the SealedNames docstring names explicitly. Fixture assembly
        // has only the renderer, no [PlangType], so pass-1 passes and the
        // gate fires on pass-2.
        var asm = System.Reflection.Assembly.LoadFrom(SignatureRendererShadowDll);
        var result = global::app.type.list.Loader.Register(asm, new global::app.type.list.@this());
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.ErrorKey).IsEqualTo("TypeLoadCollision");
        await Assert.That(result.ErrorMessage).Contains("signature");
    }

    [Test] public async Task LoadDll_InferredSealedName_FailsWith_TypeLoadCollision()
    {
        // Pass-1 @this-convention inferred-name branch — the loaded assembly
        // declares a `this`-named class in namespace `*.callback`, so
        // InferName yields "callback" (a sealed name). The gate refuses
        // before the registry is touched.
        var asm = System.Reflection.Assembly.LoadFrom(CallbackInferredShadowDll);
        var result = global::app.type.list.Loader.Register(asm, new global::app.type.list.@this());
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.ErrorKey).IsEqualTo("TypeLoadCollision");
        await Assert.That(result.ErrorMessage).Contains("callback");
    }

    [Test] public async Task SealedNames_AreCaseInsensitive_AndCoverCoreSigningTypes()
    {
        // The carve-out covers the names the signing pipeline assumes are
        // built-in. Lookup is OrdinalIgnoreCase so a DLL declaring
        // `[PlangType("Identity")]` can't slip past the comparison.
        var sealedSet = global::app.type.list.Loader.SealedNames;
        await Assert.That(sealedSet.Contains("identity")).IsTrue();
        await Assert.That(sealedSet.Contains("IDENTITY")).IsTrue();
        await Assert.That(sealedSet.Contains("signature")).IsTrue();
        await Assert.That(sealedSet.Contains("signedoperation")).IsTrue();
        await Assert.That(sealedSet.Contains("callback")).IsTrue();
        await Assert.That(sealedSet.Contains("channel")).IsTrue();
        // Primitives stay overridable — their body is constrained by the type.
        await Assert.That(sealedSet.Contains("int")).IsFalse();
        await Assert.That(sealedSet.Contains("string")).IsFalse();
        await Assert.That(sealedSet.Contains("path")).IsFalse();
    }

    [Test] public async Task ITypeRenderer_InterfaceShape_FormatPropertyAndWriteMethod()
    {
        var t = typeof(global::app.type.list.ITypeRenderer);
        await Assert.That(t.GetProperty("Format")).IsNotNull();
        await Assert.That(t.GetProperty("TypeName")).IsNotNull();
        await Assert.That(t.GetMethod("Write")).IsNotNull();
        var writeParams = t.GetMethod("Write")!.GetParameters();
        await Assert.That(writeParams.Length).IsEqualTo(2);
        await Assert.That(writeParams[0].ParameterType).IsEqualTo(typeof(object));
        await Assert.That(writeParams[1].ParameterType).IsEqualTo(typeof(global::app.channel.serializer.IWriter));
    }

    private sealed class FakeWriter : global::app.channel.serializer.IWriter
    {
        public FakeWriter(string format) { Format = format; }
        public string Format { get; }
        public void Null() { }
        public void Bool(bool v) { }
        public void Int(int v) { }
        public void Long(long v) { }
        public void Float(float v) { }
        public void Double(double v) { }
        public void String(string v) { }
        public void DateTime(System.DateTime v) { }
        public void DateTimeOffset(System.DateTimeOffset v) { }
        public void TimeSpan(System.TimeSpan v) { }
        public void Guid(System.Guid v) { }
        public void Enum(System.Enum v) { }
        public void Decimal(decimal v) { }
        public void Bytes(byte[] v) { }
        public void BeginArray(int c) { }
        public void BeginObject() { }
        public void Name(string n) { }
        public void EndObject() { }
        public void EndArray() { }
        public void BeginRecord(global::app.data.@this r) { }
        public void EndRecord() { }
        public void Value(object? n) { }
    }
}
