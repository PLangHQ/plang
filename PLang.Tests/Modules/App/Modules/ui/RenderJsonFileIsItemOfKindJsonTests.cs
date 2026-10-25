using app;
using app.module.ui;
using app.module.ui.code;

namespace PLang.Tests.App.Modules.ui;

// Stage 4 (kind-is-type, decisions 773 + 792): a rendered `.json` file is an item of kind json —
// Type.Name is "item" and Type.kind.Name is "json" (json is item's, 773); an inline template stays kindless.
public class RenderJsonFileIsItemOfKindJsonTests : IDisposable
{
    private readonly string _tempDir;
    private readonly global::app.@this _app;
    private readonly global::app.module.ui.code.Fluid _provider;

    public RenderJsonFileIsItemOfKindJsonTests()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_render_itemjson_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = new global::app.@this(_tempDir, global::PLang.Tests.Parent.Actor).TestSigning();
        _provider = new global::app.module.ui.code.Fluid();
    }

    public void Dispose()
    {
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        if (System.IO.Directory.Exists(_tempDir)) System.IO.Directory.Delete(_tempDir, true);
    }

    private void WriteTemplateFile(string relativePath, string content)
    {
        var fullPath = System.IO.Path.Combine(_tempDir, relativePath);
        var dir = System.IO.Path.GetDirectoryName(fullPath);
        if (dir != null) System.IO.Directory.CreateDirectory(dir);
        System.IO.File.WriteAllText(fullPath, content);
    }

    [Test]
    public async Task Render_AJsonFile_IsItemOfKindJson()
    {
        WriteTemplateFile("request.json", "{\"deep\": \"{{ model }}\"}");
        var context = _app.actor.list.User.Context;
        context.Variable.Set(new Data("model", "m-1", context: context));

        var file = await _provider.Render(new Render(context) { Template = (global::app.type.item.text.@this)"request.json", IsFile = (global::app.type.item.@bool.@this)true });
        var inline = await _provider.Render(new Render(context) { Template = (global::app.type.item.text.@this)"{\"deep\": \"{{ model }}\"}", IsFile = (global::app.type.item.@bool.@this)false });

        await file.IsSuccess();
        await Assert.That((await file.Value())?.ToString()).IsEqualTo("{\"deep\": \"m-1\"}");
        await Assert.That(file.Type.Name).IsEqualTo("item");
        await Assert.That(file.Type.kind.Name).IsEqualTo("json");
        await Assert.That(inline.Type.kind.IsEmpty).IsTrue();
    }
}
