namespace PLang.Tests.App.CompareRedesign;

// Stage 6 — every comparison consumer routes through `data.Compare(other)` +
// the boundary mapping. `if` operators, `assert`, two-phase async `sort`,
// list ops; Pile-2 decompose sites switch to typed methods (no `ToRaw`
// escape); the old mediator/coercion/interfaces are deleted. Membership
// (`contains`/`in`/`indexof`/`unique`) matches only on `Equal`, never errors.
public class Stage6_ConsumersTests
{
    // ---------- operators + assert ----------

    [Test]
    public async Task IfEquals_BoundaryMap_EqualTrue_NotEqualFalse_IncomparableError()
    {
        // == : Equal→true, NotEqual→false, Incomparable→error
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task IfLess_BoundaryMap_LessTrue_NotEqualError_IncomparableError()
    {
        // < : Less→true, NotEqual→error, Incomparable→error
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Assert_Equals_AwaitsCompareAndAppliesBoundary()
    {
        // assert/code/Default.cs Equals/NotEquals/GreaterThan/LessThan/Contains/NotContains await Compare and map per the table
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    // ---------- sort ----------

    [Test]
    public async Task Sort_TwoPhase_KeysMaterialiseAsync_OrderSync_NoGetResult()
    {
        // phase 1 awaits all keys; phase 2 sync sort with no await inside the comparator — no GetAwaiter().GetResult()
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task SortBySize_FilesStatInPhaseOne_OrderInPhaseTwo()
    {
        // sort %files% by size — all stat reads happen in phase 1; phase 2 orders in-hand keys
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task ComparerObjectDefault_NotUsedAnywhere_GrepGate()
    {
        // sort.cs no longer references Comparer<object>.Default — uses the typed Compare pipeline
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    // ---------- membership (never errors) ----------

    [Test]
    public async Task ListContains_MatchesOnEqualOnly_TypeMismatchNoMatch()
    {
        // [%dict%] contains %number% → false, no error (Incomparable element treated as no-match)
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task ListIndexOf_NotFound_Returns_MinusOne_NeverError()
    {
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task ListUnique_TreatsNotEqualAndIncomparableAsNoMatch()
    {
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    // ---------- Pile-2 ----------

    [Test]
    public async Task Pile2_SqliteSettings_BindsSerializedBlob_NoToRaw()
    {
        // settings/Sqlite.cs:235 — Store returns the json blob directly; no text-wrap, no ToRaw collapse line
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Pile2_OpenAiCache_NavigatesDict_NoDictionaryCopy()
    {
        // llm/OpenAi.cs:1003 — keys read off the dict via navigation, not a Dictionary copy
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Pile2_Identity_RoundTripsJson_NoNativeDictIntermediary()
    {
        // identity/Default.cs:325 — Identity↔json (STJ), no native-dict step
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Pile2_Fluid_RendersViaTextSerializer_NoToRaw()
    {
        // ui/Fluid.cs:82 — value rendered through the text serializer; no ToRaw copy
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    // ---------- demolition (the things that must NOT exist) ----------

    [Test]
    public async Task OldMediator_AppDataCompare_Static_Deleted()
    {
        // reflection: the static `app.data.Compare` mediator (Cmp.Order/...) is gone — Compare lives on Data
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task ScalarComparer_Deleted()
    {
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task OperatorNormalizeTypes_Deleted()
    {
        // Operator.NormalizeTypes + IsTextLike/IsNumberLike removed; coercion lives on the driving type
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task IEquatableValue_IOrderableValue_Deleted()
    {
        // unified onto Compare → Comparison; the old interfaces and per-type AreEqual/Order are removed
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
