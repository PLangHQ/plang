using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules;

/// <summary>
/// Guards <c>app.goal.step.action.@this.Return</c>.
///
/// The catalog row a built action carries surfaces the PLang type of T from
/// <c>Run()</c>'s declared return type. Compile.llm uses this to pick the
/// type-stamp for a trailing <c>variable.set</c> after a <c>write to %x%</c>.
///
/// A regression where <c>Return</c> goes null (or to the wrong PLang
/// name) silently mis-types %result%, which is exactly the build-time
/// mis-compile the builder bot reported in the Class 2/3 regressions during
/// the typed-returns sweep. The whole point of the sweep was this signal.
///
/// These tests pin the contract against a representative slice of the live
/// catalog so a future refactor of <c>DescribeReturnTypeName</c> can't drop
/// rows without going red.
/// </summary>
public class ModulesDescribeReturnTypeTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_returntype_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = TestApp.Create(_tempDir);
        _app.Build = new global::app.module.action.build.@this(_app.System.Context);
    }

    [After(Test)]
    public async Task Cleanup()
    {
        try
        {
            await _app.DisposeAsync();
            if (System.IO.Directory.Exists(_tempDir))
                System.IO.Directory.Delete(_tempDir, true);
        }
        catch { /* best effort */ }
    }

    private global::app.goal.step.action.@this Find(string module, string action)
        => _app.Module[module][action]
           ?? throw new InvalidOperationException($"catalog missing {module}.{action} — fixture stale");

    // Bare Task<Data> — polymorphic. Both bare Data and Data<object> are the
    // item type: the value type is unknown statically.
    [Test]
    public async Task Return_BareData_IsItem()
    {
        // variable.set returns bare Task<Data>.
        var row = Find("variable", "set");
        await Assert.That(row.Return).IsEqualTo(_app.Type["item"]);
    }

    // Data<global::app.type.item.@bool.@this>
    [Test]
    public async Task Return_DataOfBool_IsBool()
    {
        // file.exists.Run() returns Task<Data<path>> — the path is the value;
        // condition.compare returns Data<global::app.type.item.@bool.@this>. Use compare to pin "bool".
        var row = Find("condition", "compare");
        await Assert.That(row.Return).IsEqualTo(_app.Type["bool"]);
    }

    // Data<path>
    [Test]
    public async Task Return_DataOfPath_IsPath()
    {
        // file.save → Task<Data<path>>.
        var row = Find("file", "save");
        await Assert.That(row.Return).IsEqualTo(_app.Type["path"]);
    }

    // Data<global::app.type.item.list.@this<path>> — generic collection rendering.
    [Test]
    public async Task Return_DataOfListOfPath_IsListOfPath()
    {
        // file.list → Task<Data<global::app.type.item.list.@this<path>>>.
        var row = Find("file", "list");
        await Assert.That(row.Return).IsEqualTo(_app.Type["list<path>"]);
    }

    // Data<Identity> — domain type. [PlangType("identity")] on the class
    // is the single source of truth and the assembly scan picks it up.
    [Test]
    public async Task Return_DataOfIdentity_IsIdentity()
    {
        var row = Find("identity", "get");
        await Assert.That(row.Return).IsEqualTo(_app.Type["identity"]);
    }

    // Data<global::app.type.item.list.@this<Identity>> — list of domain type.
    [Test]
    public async Task Return_DataOfListOfIdentity_IsListOfIdentity()
    {
        var row = Find("identity", "list");
        await Assert.That(row.Return).IsEqualTo(_app.Type["list<identity>"]);
    }

    // Sanity: every catalog row carries a type — item or a real T.
    [Test]
    public async Task Return_AllCatalogRows_HaveAValue()
    {
        var catalog = _app.Module.Names
            .SelectMany(n => _app.Module.GetActions(n).Select(a => _app.Module[n][a]!));
        var missing = catalog.Where(a => a.Return == null)
                             .Select(a => $"{a.Module}.{a.Name}")
                             .ToList();

        await Assert.That(missing.Count)
            .IsEqualTo(0)
            .Because($"actions missing Return: {string.Join(", ", missing)}");
    }
}
