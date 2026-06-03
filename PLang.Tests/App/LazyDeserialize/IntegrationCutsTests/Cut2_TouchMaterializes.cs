using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.IntegrationCutsTests;

// Cut 2 — touch materialises correctly. A `config.json` read as
// `{text, json}`; a number read as `{number, biginteger}`; an image read
// as `{image, png}`. Untouched, `%cfg%` is the json *string*. Touched,
// `%cfg.port%` materialises and returns the parsed field.
public class Cut2_TouchMaterializes
{
    [Test] public async Task Cut2_ConfigJson_UntouchedIsTextString_NavigatedReturnsField() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task Cut2_BigIntegerString_ReadsLossless_OnArithmetic() { throw new System.NotImplementedException("not implemented"); }

    // The image materialises only when a property is read (e.g. width),
    // not at read time.
    [Test] public async Task Cut2_ImagePng_MaterializesOnly_WhenWidthRead() { throw new System.NotImplementedException("not implemented"); }
}
