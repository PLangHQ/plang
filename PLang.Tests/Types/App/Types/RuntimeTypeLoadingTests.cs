namespace PLang.Tests.App.Types;

// plang-types — Stage 7
// `- load X.dll` scans the assembly for [PlangType] classes → Registry.RegisterRuntime.
// A value writes itself, so a loaded type needs no renderer. Sealed built-in
// names may not be claimed by a runtime-loaded type.
//
// Tests use the in-test fixture assembly via app.type.list.Loader (the static helper
// behind code.load) — no real DLL roundtrip needed to verify the wiring.

public class RuntimeTypeLoadingTests
{
    [global::app.Attributes.PlangType("runtime-fixture-only")]
    public sealed class FixtureOnly : global::app.type.item.@this
    {
        public static string Example => "x";
        public static string Shape => "string";
    }

    // We isolate each scan to a freshly built fixture assembly so the runtime
    // overrides don't leak between tests. Use the test assembly directly —
    // its [PlangType] fixtures live in this file.
    private static System.Reflection.Assembly TestAssembly =>
        typeof(RuntimeTypeLoadingTests).Assembly;

    private static global::app.actor.context.@this Ctx => global::PLang.Tests.TestApp.SharedContext;

    [Test] public async Task LoadDll_PlangTypeClass_RegistersViaRegistryRegisterRuntime()
    {
        var types = new global::app.type.list.@this();
        types.Add(TestAssembly, Ctx);
        // The fixture types in this file should be in the registry.
        await Assert.That(types.Contains("runtime-fixture-only")).IsTrue();
        await Assert.That(types.Clr("runtime-fixture-only")).IsEqualTo(typeof(FixtureOnly));
    }

    [Test] public async Task Register_AnOwnedName_FailsWithTheOneNameError()
    {
        // One name, one class: "string" is owned (by text), so a runtime registration of it fails loudly.
        var types = new global::app.type.list.@this();
        var result = types.Add(typeof(System.Uri), Ctx, "string");
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error?.Message).Contains("one name, one class");
        await Assert.That(types.Clr("string")).IsEqualTo(typeof(global::app.type.item.text.@this));
    }

    [Test] public async Task Register_ABuiltInTypesOwnName_FailsWithTheOneNameError()
    {
        // A built-in type cannot be replaced by a runtime registration — "number" stays number's.
        var types = new global::app.type.list.@this();
        var result = types.Add(typeof(System.Uri), Ctx, "number");
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error?.Message).Contains("one name, one class");
        await Assert.That(types.Clr("number")).IsEqualTo(typeof(global::app.type.item.number.@this));
    }

    private static string FixtureDll(string name) => System.IO.Path.GetFullPath(
        System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..",
            "..", "Shared", "Fixtures", "dlls", name));

    private static readonly string IdentityShadowDll = FixtureDll("IdentityShadow.dll");
    private static readonly string CallbackInferredShadowDll = FixtureDll("CallbackInferredShadow.dll");

    [Test] public async Task LoadDll_AttemptToShadowSealedName_FailsWith_TypeLoadCollision()
    {
        // Pass-1 explicit [PlangType("identity")] — sealed-name gate refuses
        // with TypeLoadCollision before the registry sees the type. Replacing
        // identity's CLR type would let a runtime DLL compose the body that
        // gets signed under the actor's key.
        var asm = System.Reflection.Assembly.LoadFrom(IdentityShadowDll);
        var result = new global::app.type.list.@this().Add(asm, Ctx);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error?.Key).IsEqualTo("TypeLoadCollision");
        await Assert.That(result.Error?.Message).Contains("identity");
    }

    // A loaded type may not claim a sealed word as an alias either. It declares no type of its own (no
    // [PlangType], not an @this) and isn't exported, so no scan of this assembly takes it in; the test names it.
    private sealed class AliasShadow : global::app.type.item.@this
    {
        public static IReadOnlyList<string> Alias => ["identity"];
    }

    [Test] public async Task AddType_ASealedWordAsAlias_FailsWith_TypeLoadCollision()
    {
        var result = new global::app.type.list.@this().Add(typeof(AliasShadow), Ctx, "aliasshadow");
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error?.Key).IsEqualTo("TypeLoadCollision");
        await Assert.That(result.Error?.Message).Contains("identity");
    }

    [Test] public async Task LoadDll_AnUndeclaredCallbackFolder_GoesByItsNamespace_NotTheSealedWord()
    {
        // The loaded assembly holds a `this`-named class in a namespace ending `.callback` and declares no
        // word. Nothing is guessed from a folder: it goes by its namespace, which claims no sealed word.
        var asm = System.Reflection.Assembly.LoadFrom(CallbackInferredShadowDll);
        var types = new global::app.type.list.@this();
        var result = types.Add(asm, Ctx);
        await result.IsSuccess();
        await Assert.That(types.Contains("callback")).IsFalse();
    }

    [Test] public async Task SealedNames_AreCaseInsensitive_AndCoverCoreSigningTypes()
    {
        // The carve-out covers the names the signing pipeline assumes are
        // built-in. Lookup is OrdinalIgnoreCase so a DLL declaring
        // `[PlangType("Identity")]` can't slip past the comparison.
        var sealedSet = new global::app.type.list.@this().Sealed;
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
}
