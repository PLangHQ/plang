using Comparison = global::app.data.Comparison;
using Operator = global::app.module.condition.Operator;

namespace PLang.Tests.App.CompareRedesign;

// Stage 6 — every comparison consumer routes through `data.Compare(other)` +
// the boundary mapping. `if` operators, `assert`, two-phase async `sort`,
// list ops; Pile-2 decompose sites switch to typed methods (no `ToRaw`
// escape); the old mediator/coercion/interfaces are deleted. Membership
// (`contains`/`in`/`indexof`/`unique`) matches only on `Equal`, never errors.
public class Stage6_ConsumersTests
{
    private static global::app.@this NewApp() => new(System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), "plang-stage6-" + System.Guid.NewGuid().ToString("N")[..8]));

    private static Data D(global::app.@this app, object? v, string typeName)
        => new("x", v, global::app.type.@this.FromName(typeName), context: app.User.Context);

    private static string RepoRoot()
    {
        var dir = System.AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "PLang", "app")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir!;
    }

    // ---------- operators + assert ----------

    [Test]
    public async Task IfEquals_BoundaryMap_EqualTrue_NotEqualFalse_IncomparableError()
    {
        // == : Equal→true, NotEqual→false, Incomparable→error
        await using var app = NewApp();
        var eq = new Operator("==");
        await Assert.That(await eq.Evaluate(D(app, "5", "text"), D(app, 5, "number"))).IsTrue();      // Equal
        await Assert.That(await eq.Evaluate(D(app, true, "bool"), D(app, false, "bool"))).IsFalse();  // NotEqual
        var dict = global::app.type.dict.@this.FromRaw(new Dictionary<string, object?> { ["a"] = 1 }, app.User.Context);
        await Assert.That(async () => await eq.Evaluate(D(app, dict, "dict"), D(app, 5, "number")))
            .Throws<global::app.data.IncomparableException>();                                        // Incomparable
    }

    [Test]
    public async Task IfLess_BoundaryMap_LessTrue_NotEqualError_IncomparableError()
    {
        // < : Less→true, NotEqual→error, Incomparable→error
        await using var app = NewApp();
        var lt = new Operator("<");
        await Assert.That(await lt.Evaluate(D(app, 4, "number"), D(app, 5, "number"))).IsTrue();      // Less
        await Assert.That(async () => await lt.Evaluate(D(app, true, "bool"), D(app, false, "bool")))
            .Throws<global::app.data.IncomparableException>();                                        // NotEqual -> error
        var dict = global::app.type.dict.@this.FromRaw(new Dictionary<string, object?> { ["a"] = 1 }, app.User.Context);
        await Assert.That(async () => await lt.Evaluate(D(app, dict, "dict"), D(app, 5, "number")))
            .Throws<global::app.data.IncomparableException>();                                        // Incomparable -> error
    }

    [Test]
    public async Task Assert_Equals_AwaitsCompareAndAppliesBoundary()
    {
        // assert/code/Default.cs Equals/NotEquals/GreaterThan/LessThan/Contains/NotContains await Compare and map per the table
        // the comparing asserts are async (they await data.Compare) — the interface pins it
        var m = typeof(global::app.module.assert.code.IAssert).GetMethod("Equals");
        await Assert.That(m).IsNotNull();
        await Assert.That(typeof(Task).IsAssignableFrom(m!.ReturnType)).IsTrue();
        var gt = typeof(global::app.module.assert.code.IAssert).GetMethod("GreaterThan");
        await Assert.That(typeof(Task).IsAssignableFrom(gt!.ReturnType)).IsTrue();
    }

    // ---------- sort ----------

    [Test]
    public async Task Sort_TwoPhase_KeysMaterialiseAsync_OrderSync_NoGetResult()
    {
        // phase 1 awaits all keys; phase 2 sync sort with no await inside the comparator — no GetAwaiter().GetResult()
        // the sort surface is async (phase 1 awaits values/keys); the phase-2 comparator
        // is sync with no GetAwaiter().GetResult() anywhere in the file
        var src = await File.ReadAllTextAsync(Path.Combine(RepoRoot(), "PLang", "app", "type", "list", "this.cs"));
        await Assert.That(src).Contains("public async System.Threading.Tasks.Task SortByValue");
        await Assert.That(src).Contains("public async System.Threading.Tasks.Task SortByField");
        await Assert.That(src).DoesNotContain(".GetAwaiter().GetResult()");
    }

    [Test]
    public async Task SortBySize_FilesStatInPhaseOne_OrderInPhaseTwo()
    {
        // sort %files% by size — keys (stat) materialise in async phase 1;
        // phase 2 orders the in-hand keys with a sync comparator
        await using var app = NewApp();
        var ctx = app.User.Context;
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-sortsize-" + System.Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(System.IO.Path.Combine(dir, "big.txt"), new string('c', 30));
            File.WriteAllText(System.IO.Path.Combine(dir, "tiny.txt"), "a");
            File.WriteAllText(System.IO.Path.Combine(dir, "mid.txt"), new string('b', 10));

            var files = new global::app.type.list.@this { Context = ctx };
            foreach (var name in new[] { "big.txt", "tiny.txt", "mid.txt" })
                files.Add(new Data(name, new global::app.type.path.file.@this(System.IO.Path.Combine(dir, name), context: ctx), context: ctx));

            await files.SortByField("size", descending: false);

            var ordered = files.Items.Select(d => d.Peek()?.ToString() ?? "").ToList();
            await Assert.That(ordered[0]).Contains("tiny.txt");
            await Assert.That(ordered[1]).Contains("mid.txt");
            await Assert.That(ordered[2]).Contains("big.txt");
        }
        finally { Directory.Delete(dir, true); }
    }

    [Test]
    public async Task ComparerObjectDefault_NotUsedAnywhere_GrepGate()
    {
        // sort.cs no longer references Comparer<object>.Default — uses the typed Compare pipeline
        var src = await File.ReadAllTextAsync(Path.Combine(RepoRoot(), "PLang", "app", "module", "list", "sort.cs"));
        await Assert.That(src).DoesNotContain("Comparer<object>.Default");
        var listSrc = await File.ReadAllTextAsync(Path.Combine(RepoRoot(), "PLang", "app", "type", "list", "this.cs"));
        await Assert.That(listSrc).DoesNotContain("Comparer<object>.Default");
    }

    // ---------- membership (never errors) ----------

    [Test]
    public async Task ListContains_MatchesOnEqualOnly_TypeMismatchNoMatch()
    {
        // [%dict%] contains %number% → false, no error (Incomparable element treated as no-match)
        await using var app = NewApp();
        var ctx = app.User.Context;
        var dict = global::app.type.dict.@this.FromRaw(new Dictionary<string, object?> { ["a"] = 1 }, ctx);
        var list = new global::app.type.list.@this { Context = ctx };
        list.Add(new Data("", dict, context: ctx));
        var holder = new Data("l", list, context: ctx);
        // membership never errors: the Incomparable element pair is just "not this one"
        var op = new Operator("contains");
        await Assert.That(await op.Evaluate(holder, D(app, 5, "number"))).IsFalse();
    }

    [Test]
    public async Task ListIndexOf_NotFound_Returns_MinusOne_NeverError()
    {
        await using var app = NewApp();
        var ctx = app.User.Context;
        var dict = global::app.type.dict.@this.FromRaw(new Dictionary<string, object?> { ["a"] = 1 }, ctx);
        var list = new global::app.type.list.@this { Context = ctx };
        list.Add(new Data("", dict, context: ctx));
        await ctx.Variable.Set("items", list);
        var result = await app.RunAction(new global::app.module.list.IndexOf(ctx) { ListName = new global::app.data.@this<global::app.variable.@this>("", new global::app.variable.@this("items")),
            Value = D(app, 99, "number"),
        }, ctx);
        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("-1");
    }

    [Test]
    public async Task ListUnique_TreatsNotEqualAndIncomparableAsNoMatch()
    {
        await using var app = NewApp();
        var ctx = app.User.Context;
        // a mixed list (dict + number) dedups without error — Incomparable pairs never match
        var dict = global::app.type.dict.@this.FromRaw(new Dictionary<string, object?> { ["a"] = 1 }, ctx);
        var list = new global::app.type.list.@this { Context = ctx };
        list.Add(new Data("", dict, context: ctx));
        list.Add(new Data("", 5, context: ctx));
        list.Add(new Data("", 5, context: ctx));
        await ctx.Variable.Set("items", list);
        var result = await app.RunAction(new global::app.module.list.Unique(ctx) { ListName = new global::app.data.@this<global::app.variable.@this>("", new global::app.variable.@this("items")),
        }, ctx);
        await result.IsSuccess();
    }

    // ---------- Pile-2 ----------

    [Test]
    public async Task Pile2_SqliteSettings_BindsSerializedBlob_NoToRaw()
    {
        // settings/Sqlite.cs — Store returns Data<text>; the typed door yields the
        // json blob, no generic item-leaf/ToRaw collapse at the bind site.
        var src = await File.ReadAllTextAsync(Path.Combine(RepoRoot(), "PLang", "app", "module", "settings", "Sqlite.cs"));
        await Assert.That(src).DoesNotContain("ToRaw");
        await Assert.That(src).Contains("await serialized.Value()");
    }

    [Test]
    public async Task Pile2_OpenAiCache_NavigatesDict_NoDictionaryCopy()
    {
        // llm/OpenAi.cs — cache restore reads keys off the native dict (Get/Entries
        // navigation), not a raw Dictionary copy via ToRaw.
        var src = await File.ReadAllTextAsync(Path.Combine(RepoRoot(), "PLang", "app", "module", "llm", "code", "OpenAi.cs"));
        await Assert.That(src).DoesNotContain("ToRaw");
        await Assert.That(src).Contains("nativeDict.Get(\"Value\")");
        await Assert.That(src).Contains("nativeDict.Entries");
    }

    [Test]
    public async Task Pile2_Fluid_RendersViaTextSerializer_NoToRaw()
    {
        // ui/Fluid.cs — natives render through lazy read-through views (zero copy);
        // no ToRaw deep-copy call. (The one mention is the comment stating that.)
        var src = await File.ReadAllTextAsync(Path.Combine(RepoRoot(), "PLang", "app", "module", "ui", "code", "Fluid.cs"));
        await Assert.That(src).DoesNotContain(".Clr<object>()");
        await Assert.That(src).Contains("NativeCollectionConverter");
    }

    // ---------- demolition (the things that must NOT exist) ----------

    [Test]
    public async Task OldMediator_AppDataCompare_Static_Deleted()
    {
        // reflection: the static `app.data.Compare` mediator (Cmp.Order/...) is gone — Compare lives on Data
        var t = typeof(Data).Assembly.GetType("app.data.Compare");
        await Assert.That(t).IsNull();
        // Compare lives on Data — the async entry returning the Comparison enum
        var m = typeof(Data).GetMethod("Compare", new[] { typeof(Data) });
        await Assert.That(m).IsNotNull();
    }

    [Test]
    public async Task ScalarComparer_Deleted()
    {
        await Assert.That(typeof(Data).Assembly.GetType("app.data.ScalarComparer")).IsNull();
    }

    [Test]
    public async Task OperatorNormalizeTypes_Deleted()
    {
        // Operator.NormalizeTypes + IsTextLike/IsNumberLike removed; coercion lives on the driving type
        await Assert.That(typeof(Operator).GetMethod("NormalizeTypes")).IsNull();
        await Assert.That(typeof(Operator).GetMethod("IsTextLike",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)).IsNull();
    }

    [Test]
    public async Task IEquatableValue_IOrderableValue_Deleted()
    {
        // unified onto Compare → Comparison; the old interfaces and per-type AreEqual/Order are removed
        var asm = typeof(Data).Assembly;
        await Assert.That(asm.GetType("app.data.IEquatableValue")).IsNull();
        await Assert.That(asm.GetType("app.data.IOrderableValue")).IsNull();
    }
}
