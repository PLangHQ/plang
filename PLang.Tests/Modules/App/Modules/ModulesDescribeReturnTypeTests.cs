using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules;

/// <summary>
/// Guards <c>app.goal.step.action.@this.ReturnTypeName</c>.
///
/// The catalog row a built action carries surfaces the PLang name of T from
/// <c>Run()</c>'s declared return type. Compile.llm uses this to pick the
/// type-stamp for a trailing <c>variable.set</c> after a <c>write to %x%</c>.
///
/// A regression where <c>ReturnTypeName</c> goes null (or to the wrong PLang
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

    private async Task<global::app.goal.step.action.@this> Find(string module, string action)
    {
        var catalog = await _app.Module.Describe();
        var row = catalog.FirstOrDefault(a => a.Module == module && a.ActionName == action);
        if (row == null)
            throw new InvalidOperationException($"catalog missing {module}.{action} — fixture stale");
        return row;
    }

    // Bare Task<Data> — polymorphic. The reflection layer flattens both
    // bare Data and Data<object> to "data" because both mean "value type
    // unknown statically." Pinning a known bare action keeps that rule honest.
    [Test]
    public async Task ReturnTypeName_BareData_IsData()
    {
        // variable.set returns bare Task<Data>.
        var row = await Find("variable", "set");
        await Assert.That(row.ReturnTypeName).IsEqualTo("data");
    }

    // Data<global::app.type.item.@bool.@this>
    [Test]
    public async Task ReturnTypeName_DataOfBool_IsBool()
    {
        // file.exists.Run() returns Task<Data<path>> — the path is the value;
        // condition.compare returns Data<global::app.type.item.@bool.@this>. Use compare to pin "bool".
        var row = await Find("condition", "compare");
        await Assert.That(row.ReturnTypeName).IsEqualTo("bool");
    }

    // Data<path>
    [Test]
    public async Task ReturnTypeName_DataOfPath_IsPath()
    {
        // file.save → Task<Data<path>>.
        var row = await Find("file", "save");
        await Assert.That(row.ReturnTypeName).IsEqualTo("path");
    }

    // Data<global::app.type.item.list.@this<path>> — generic collection rendering.
    [Test]
    public async Task ReturnTypeName_DataOfListOfPath_IsListOfPath()
    {
        // file.list → Task<Data<global::app.type.item.list.@this<path>>>.
        var row = await Find("file", "list");
        await Assert.That(row.ReturnTypeName).IsEqualTo("list<path>");
    }

    // Data<Identity> — domain type. [PlangType("identity")] on the class
    // is the single source of truth and the assembly scan picks it up.
    [Test]
    public async Task ReturnTypeName_DataOfIdentity_IsIdentity()
    {
        var row = await Find("identity", "get");
        await Assert.That(row.ReturnTypeName).IsEqualTo("identity");
    }

    // Data<global::app.type.item.list.@this<Identity>> — list of domain type.
    [Test]
    public async Task ReturnTypeName_DataOfListOfIdentity_IsListOfIdentity()
    {
        var row = await Find("identity", "list");
        await Assert.That(row.ReturnTypeName).IsEqualTo("list<identity>");
    }

    // Sanity: every catalog row carries a non-empty value (a row's
    // ReturnTypeName should never silently go null at build time —
    // either "data" or a real T name).
    [Test]
    public async Task ReturnTypeName_AllCatalogRows_HaveAValue()
    {
        var catalog = await _app.Module.Describe();
        var missing = catalog.Where(a => string.IsNullOrEmpty(a.ReturnTypeName))
                             .Select(a => $"{a.Module}.{a.ActionName}")
                             .ToList();

        await Assert.That(missing.Count)
            .IsEqualTo(0)
            .Because($"actions missing ReturnTypeName: {string.Join(", ", missing)}");
    }
}
