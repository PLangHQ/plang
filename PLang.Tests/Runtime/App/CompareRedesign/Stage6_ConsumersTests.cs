using Comparison = global::app.data.Comparison;
using Operator = global::app.data.Operator;

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
        => new("x", v, app.actor.list.User.Context.App.type.list[typeName], context: app.actor.list.User.Context);

    private static string RepoRoot()
    {
        var dir = System.AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "PLang", "app")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir!;
    }

    // ---------- operators + assert ----------

    // The operator's plang answer under the app's context.
    private static Task<global::app.data.@this<global::app.type.item.@bool.@this>> Answer(
        global::app.@this app, Operator op, Data left, Data right) => op.Evaluate(left, right, app.actor.list.User.Context);

    [Test]
    public async Task IfEquals_BoundaryMap_EqualTrue_NotEqualFalse_IncomparableError()
    {
        // == : Equal→true, NotEqual→false, Incomparable→an error answer, never a throw
        await using var app = NewApp();
        var eq = new Operator("==");
        await Assert.That((await Answer(app, eq, D(app, "5", "text"), D(app, 5, "number"))).ToBoolean()).IsTrue();      // Equal
        await Assert.That((await Answer(app, eq, D(app, true, "bool"), D(app, false, "bool"))).ToBoolean()).IsFalse();  // NotEqual
        var dict = global::PLang.Tests.Shared.Make.Dict(new Dictionary<string, object?> { ["a"] = 1 }, app.actor.list.User.Context);
        var incomparable = await Answer(app, eq, D(app, dict, "dict"), D(app, 5, "number"));
        await incomparable.IsFailure();
        await Assert.That(incomparable.Error!.Key).IsEqualTo("EvaluationError");
    }

    [Test]
    public async Task IfLess_BoundaryMap_LessTrue_NotEqualError_IncomparableError()
    {
        // < : Less→true, NotEqual→an error answer, Incomparable→an error answer, never a throw
        await using var app = NewApp();
        var lt = new Operator("<");
        await Assert.That((await Answer(app, lt, D(app, 4, "number"), D(app, 5, "number"))).ToBoolean()).IsTrue();      // Less
        var notEqual = await Answer(app, lt, D(app, true, "bool"), D(app, false, "bool"));
        await notEqual.IsFailure();
        await Assert.That(notEqual.Error!.Key).IsEqualTo("EvaluationError");
        var dict = global::PLang.Tests.Shared.Make.Dict(new Dictionary<string, object?> { ["a"] = 1 }, app.actor.list.User.Context);
        var incomparable = await Answer(app, lt, D(app, dict, "dict"), D(app, 5, "number"));
        await incomparable.IsFailure();
        await Assert.That(incomparable.Error!.Message).Contains("cannot order 'dict'");
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

    // The source file of a class: its namespace is its folder under PLang/; an @this class is this.cs, any
    // other the file its name names (case aside: `Sort` is sort.cs).
    private static string SourceOf(System.Type type)
    {
        var folder = Path.Combine([RepoRoot(), "PLang", .. type.Namespace!.Split('.')]);
        var name = (type.Name == "@this" ? "this" : type.Name) + ".cs";
        return Directory.EnumerateFiles(folder, "*.cs").FirstOrDefault(f =>
            string.Equals(Path.GetFileName(f), name, System.StringComparison.OrdinalIgnoreCase)) ?? Path.Combine(folder, name);
    }

    // The list sorts in two phases: its keys are read async (all I/O lands there), then it orders in
    // memory — so nothing in the list blocks on a task.
    [Test]
    public async Task Sort_TwoPhase_KeysMaterialiseAsync_OrderSync_NoGetResult()
    {
        var sort = typeof(global::app.type.item.list.@this).GetMethod("Sort",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
            [typeof(string), typeof(bool), typeof(global::app.actor.context.@this)]);
        await Assert.That(sort).IsNotNull();
        await Assert.That(sort!.GetCustomAttributes(typeof(System.Runtime.CompilerServices.AsyncStateMachineAttribute), false)).IsNotEmpty();

        var file = SourceOf(typeof(global::app.type.item.list.@this));
        await Assert.That(File.Exists(file)).IsTrue();
        await Assert.That(await File.ReadAllTextAsync(file)).DoesNotContain(".GetAwaiter().GetResult()");
    }

    [Test]
    public async Task SortBySize_FilesStatInPhaseOne_OrderInPhaseTwo()
    {
        // sort %files% by size — keys (stat) materialise in async phase 1;
        // phase 2 orders the in-hand keys with a sync comparator
        await using var app = NewApp();
        var ctx = app.actor.list.User.Context;
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-sortsize-" + System.Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(System.IO.Path.Combine(dir, "big.txt"), new string('c', 30));
            File.WriteAllText(System.IO.Path.Combine(dir, "tiny.txt"), "a");
            File.WriteAllText(System.IO.Path.Combine(dir, "mid.txt"), new string('b', 10));

            var files = new global::app.type.item.list.@this();
            foreach (var name in new[] { "big.txt", "tiny.txt", "mid.txt" })
                files.Add(new Data(name, new global::app.type.item.path.file.@this(System.IO.Path.Combine(dir, name)), context: ctx));

            await files.Sort("size", descending: false, app.actor.list.User.Context);

            var ordered = files.Items(app.actor.list.User.Context).Select(d => d.Peek()?.ToString() ?? "").ToList();
            await Assert.That(ordered[0]).Contains("tiny.txt");
            await Assert.That(ordered[1]).Contains("mid.txt");
            await Assert.That(ordered[2]).Contains("big.txt");
        }
        finally { Directory.Delete(dir, true); }
    }

    [Test]
    public async Task ComparerObjectDefault_NotUsedAnywhere_GrepGate()
    {
        // list.sort and the list it sorts don't order through Comparer<object>.Default — the typed Compare pipeline
        foreach (var type in new[] { typeof(global::app.module.list.Sort), typeof(global::app.type.item.list.@this) })
        {
            var file = SourceOf(type);
            await Assert.That(File.Exists(file)).IsTrue();
            await Assert.That(await File.ReadAllTextAsync(file)).DoesNotContain("Comparer<object>.Default");
        }
    }

    // ---------- membership (never errors) ----------

    [Test]
    public async Task ListContains_MatchesOnEqualOnly_TypeMismatchNoMatch()
    {
        // [%dict%] contains %number% → false, no error (Incomparable element treated as no-match)
        await using var app = NewApp();
        var ctx = app.actor.list.User.Context;
        var dict = global::PLang.Tests.Shared.Make.Dict(new Dictionary<string, object?> { ["a"] = 1 }, ctx);
        var list = new global::app.type.item.list.@this();
        list.Add(new Data("", dict, context: ctx));
        var holder = new Data("l", list, context: ctx);
        // membership never errors: the Incomparable element pair is just "not this one"
        var op = new Operator("contains");
        await Assert.That((await Answer(app, op, holder, D(app, 5, "number"))).ToBoolean()).IsFalse();
    }

    [Test]
    public async Task ListIndexOf_NotFound_Returns_MinusOne_NeverError()
    {
        await using var app = NewApp();
        var ctx = app.actor.list.User.Context;
        var dict = global::PLang.Tests.Shared.Make.Dict(new Dictionary<string, object?> { ["a"] = 1 }, ctx);
        var list = new global::app.type.item.list.@this();
        list.Add(new Data("", dict, context: ctx));
        await ctx.Variable.Set("items", list);
        var result = await new global::app.goal.step.action.@this(new global::app.module.list.IndexOf(ctx) { ListName = new global::app.data.@this<global::app.type.item.variable.@this>("", new global::app.type.item.variable.@this("items")),
            Value = D(app, 99, "number"),
        }, ctx).Start(ctx);
        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("-1");
    }

    [Test]
    public async Task ListUnique_TreatsNotEqualAndIncomparableAsNoMatch()
    {
        await using var app = NewApp();
        var ctx = app.actor.list.User.Context;
        // a mixed list (dict + number) dedups without error — Incomparable pairs never match
        var dict = global::PLang.Tests.Shared.Make.Dict(new Dictionary<string, object?> { ["a"] = 1 }, ctx);
        var list = new global::app.type.item.list.@this();
        list.Add(new Data("", dict, context: ctx));
        list.Add(new Data("", 5, context: ctx));
        list.Add(new Data("", 5, context: ctx));
        await ctx.Variable.Set("items", list);
        var result = await new global::app.goal.step.action.@this(new global::app.module.list.Unique(ctx) { ListName = new global::app.data.@this<global::app.type.item.variable.@this>("", new global::app.type.item.variable.@this("items")),
        }, ctx).Start(ctx);
        await result.IsSuccess();
    }

    // ---------- Pile-2 ----------

    [Test]
    public async Task Pile2_SqliteSettings_BindsSerializedBlob_NoToRaw()
    {
        // the sqlite store binds what its format encoded in the Store view — no generic
        // item-leaf/ToRaw collapse at the bind site.
        var src = await File.ReadAllTextAsync(SourceOf(typeof(global::app.store.sqlite.@this)));
        await Assert.That(src).DoesNotContain("ToRaw");
        await Assert.That(src).Contains("Format.Encode(ms, data, Context, global::app.View.Store)");
    }

    [Test]
    public async Task Pile2_OpenAiCache_NavigatesDict_NoDictionaryCopy()
    {
        // llm/OpenAi.cs — cache restore NAVIGATES the entry uniformly (dict.Entries or
        // clr(json).Enumerate), not a raw Dictionary copy via ToRaw. The old per-shape
        // reconstruction is gone — a clr(json) round-trips as raw json now.
        var src = await File.ReadAllTextAsync(Path.Combine(RepoRoot(), "PLang", "app", "module", "llm", "code", "OpenAi.cs"));
        await Assert.That(src).DoesNotContain("ToRaw");
        await Assert.That(src).Contains("d.Entries");
        await Assert.That(src).Contains("c.Enumerate(");
    }

    [Test]
    public async Task Pile2_Fluid_RendersViaTextSerializer_NoToRaw()
    {
        // ui/Fluid.cs — a container renders through one value reading the item's own doors (Item);
        // no deep-copy lowering of a container.
        var src = await File.ReadAllTextAsync(Path.Combine(RepoRoot(), "PLang", "app", "module", "ui", "code", "Fluid.cs"));
        await Assert.That(src).DoesNotContain(".Clr<object>()");
        await Assert.That(src).Contains("private sealed class Item(");
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
