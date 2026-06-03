using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.IntegrationCutsTests;

// Cut 2 — touch materialises correctly. A `config.json` read as
// `{object, json}`; a `report.csv` read as `{table, csv}`; a number read
// as `{number, biginteger}`; an image read as `{image, png}`. **Stamping
// the type does not parse** — untouched, `%cfg%` is still the json
// *string* even though `type=object`. Touched, `%cfg.port%` materialises
// and returns the parsed field. The csv navigates by row/column once
// touched.
public class Cut2_TouchMaterializes
{
    // Untouched, the value is the raw json string; the type stamp
    // (`object`) is a promise about shape, not a parse trigger.
    [Test] public async Task Cut2_ConfigJson_UntouchedIsRawString_NavigatedReturnsField() { throw new System.NotImplementedException("not implemented"); }

    // The new `table` row — csv lands `{table, csv}`. Untouched it's the
    // raw csv text; once navigated, the `(table, csv)` reader produces a
    // grid and a row/column resolves.
    [Test] public async Task Cut2_ReportCsv_UntouchedIsRawString_NavigatedReturnsRowColumn() { throw new System.NotImplementedException("not implemented"); }

    [Test] public async Task Cut2_BigIntegerString_ReadsLossless_OnArithmetic() { throw new System.NotImplementedException("not implemented"); }

    // The image materialises only when a property is read (e.g. width),
    // not at read time.
    [Test] public async Task Cut2_ImagePng_MaterializesOnly_WhenWidthRead() { throw new System.NotImplementedException("not implemented"); }
}
