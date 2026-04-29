namespace PLang.Tests.Generator.Matrix.Plain;

// Matrix entries for "plain" (non-nullable, no default) Data<T> properties.
// Each test class corresponds to a handler under Matrix/Plain/<Name>.cs (coder builds the handlers + fixture).
// Bodies are Assert.Fail("Not implemented") — the comment above each [Test] is the spec.
//
// v4 contract: the property getter resolves by calling
//   __action.GetParameter("Name", Context).As<T>(Context)
// and caches the typed Data<T> in a per-call backing field.

public class StringPlainTests
{
    // Literal string Parameter Value → property exposes Data<string> with the same value.
    [Test] public async Task StringPlain_LiteralValue_ResolvesToTypedData() => Assert.Fail("Not implemented");

    // Property is read twice in same call → second read returns the cached backing-field instance (reference equality).
    [Test] public async Task StringPlain_ReadTwice_ReturnsCachedBackingField() => Assert.Fail("Not implemented");

    // Parameter Value is empty string "" → property reads as Data<string> with "" (not null, not default).
    [Test] public async Task StringPlain_EmptyString_PreservedAsEmpty() => Assert.Fail("Not implemented");
}

public class IntPlainTests
{
    // Parameter Value of "42" (string) → typed Data<int> via TypeMapping conversion.
    [Test] public async Task IntPlain_StringValue_ConvertsToInt() => Assert.Fail("Not implemented");

    // Parameter Value of 42 (boxed int) → fast-path wrap (Value is T already), no TypeMapping.
    [Test] public async Task IntPlain_IntValue_FastPath() => Assert.Fail("Not implemented");

    // Parameter Value "not-a-number" → As<int> returns Data.FromError; property surfaces the error.
    [Test] public async Task IntPlain_UnconvertibleString_SurfacesFromError() => Assert.Fail("Not implemented");
}

public class BoolPlainTests
{
    // Parameter Value "true" → typed Data<bool> via TypeMapping.
    [Test] public async Task BoolPlain_StringTrue_ConvertsToBool() => Assert.Fail("Not implemented");

    // Parameter Value true (boxed bool) → fast-path, same Data<bool>.
    [Test] public async Task BoolPlain_BoolValue_FastPath() => Assert.Fail("Not implemented");
}

public class PathPlainTests
{
    // FileSystem.Path has static Resolve(string, Context) → As<T> dispatches to it for string Parameter Values.
    [Test] public async Task PathPlain_StringValue_UsesStaticResolve() => Assert.Fail("Not implemented");

    // Parameter Value already a FileSystem.Path → fast-path wrap, Resolve is not called.
    [Test] public async Task PathPlain_PathValue_FastPath() => Assert.Fail("Not implemented");
}
