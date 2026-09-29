using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.SingularNamespaces.AccessorTests;

// app.module is the app's module: app.module.Get("file") selects, app.module.list enumerates. A module is
// never inside anything, so the element's Current answers nothing a program walks into.
public class ModuleAccessorTests
{
    [Test] public async Task AppModule_GetByName_SelectsTheModuleElement()
    {
        await using var app = new global::app.@this("/test").Testing();
        var found = await app.module.Get("file");
        await found.IsSuccess();
        var file = await found.Value();
        await Assert.That(file).IsNotNull();
        await Assert.That(file!.Name).IsEqualTo("file");
    }

    [Test] public async Task AppModule_GetIgnoresCase()
    {
        await using var app = new global::app.@this("/test").Testing();
        var found = await app.module.Get("FILE");
        await found.IsSuccess();
        await Assert.That((await found.Value())!.Name).IsEqualTo("file");
    }

    [Test] public async Task AppModuleList_Enumerates_LoadedModules()
    {
        await using var app = new global::app.@this("/test").Testing();
        var names = app.module.list.Items().Select(m => m.Name).ToList();
        await Assert.That(names.Contains("file")).IsTrue();
        await Assert.That(names.Contains("variable")).IsTrue();
        await Assert.That(app.module.list.CountRaw).IsGreaterThan(0);
    }

    [Test] public async Task AppModule_ResolvesAction_UnderTheNewShape()
    {
        await using var app = new global::app.@this("/test").Testing();
        var file = await (await app.module.Get("file")).Value();
        await Assert.That(file!["read"]).IsNotNull();
    }

    [Test] public async Task AppModule_GetOfUnknownName_Fails()
    {
        await using var app = new global::app.@this("/test").Testing();
        var found = await app.module.Get("nope");
        await Assert.That(found.Success).IsFalse();
    }

    // The builder's Decide reads the modules as %!app.module.list%.
    [Test] public async Task AppModuleList_ReadsAsAVariable()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        var variable = new global::app.type.item.variable.parser.@this("%!app.module.list%").Variable.Single();
        var read = await variable.Start(ctx);
        await read.IsSuccess();
        await Assert.That(read.Peek()).IsSameReferenceAs(app.module.list);
    }

    [Test] public async Task Module_NamedUnknown_IsNull()
    {
        await using var app = new global::app.@this("/test").Testing();
        await Assert.That(app.module.Named("nope")).IsNull();
    }
}
