namespace PLang.Tests.App.Types;

/// <summary>
/// A type that declares it has kinds (<c>[Kinds]</c>) owns its subclasses as kinds, never as types of their own:
/// a file is a path, a class of settings is a setting, a typed list is a list, a where is a query.
/// </summary>
public class KindsTests
{
    private readonly global::app.@this _app = new global::app.@this("/test").Testing();

    [After(Test)]
    public async Task Dispose() => await _app.DisposeAsync();

    private string NameOf(System.Type clr) => _app.type.list[clr].Name;

    [Test] public async Task AFilePath_IsAPath() => await Assert.That(NameOf(typeof(global::app.type.item.path.file.@this))).IsEqualTo("path");
    [Test] public async Task AnHttpPath_IsAPath() => await Assert.That(NameOf(typeof(global::app.type.item.path.http.@this))).IsEqualTo("path");
    [Test] public async Task AClassOfSettings_IsASetting() => await Assert.That(NameOf(typeof(global::app.setting.@this))).IsEqualTo("setting");
    [Test] public async Task ATypedList_IsAList() => await Assert.That(NameOf(typeof(global::app.error.list.@this))).IsEqualTo("list");
    [Test] public async Task AWhere_IsAQuery() => await Assert.That(NameOf(typeof(global::app.module.list.type.query.where.@this))).IsEqualTo("query");

    [Test]
    public async Task NoClause_IsATypeOfItsOwn()
    {
        foreach (var clause in new[] { "where", "group", "distinct", "order" })
            await Assert.That(_app.type.list.Items().Any(t => t.Names(clause))).IsFalse();
    }
}
