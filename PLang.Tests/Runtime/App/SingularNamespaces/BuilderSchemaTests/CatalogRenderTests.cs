using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Render = global::app.module.action.ui.Render;

namespace PLang.Tests.App.SingularNamespaces.BuilderSchemaTests;

/// <summary>
/// The stepActionDetails template walks the module-discovery catalog surface
/// (%!app.module.action.list%) reading a.Cacheable, a.Properties, a.Return, and
/// module/action prose. Catalog actions are minted WITH their catalog Context
/// (action.this.Schema.Context) — the prose/Properties doors require it. These pin
/// that a clean catalog action renders; a Context-less action (a .pr-zoom action, or
/// one whose [JsonIgnore] Context was stripped by a round-trip) fails module prose.
/// </summary>
public class CatalogRenderTests
{
    private static async Task<string> RenderOverCatalog(app.@this app, string template)
    {
        var ctx = app.System.Context;
        ctx.Variable.Set(new global::app.data.@this("actions", app.Module.action.list, context: ctx));
        var action = new Render(ctx)
        {
            Template = (global::app.type.item.text.@this)template,
            IsFile = (global::app.type.item.@bool.@this)false,
        };
        var result = await new global::app.module.action.ui.code.Fluid().Render(action);
        var outp = (await result.Value())?.ToString() ?? "";
        return $"success={result.Success} err={result.Error?.Message} out=[{outp}]";
    }

    [Test] public async Task Cacheable_And_Properties_Navigate()
    {
        await using var app = global::PLang.Tests.TestApp.Create("/test");
        var r = await RenderOverCatalog(app,
            "{% for a in actions %}{{ a.Name }}:{% unless a.Cacheable %}NC{% endunless %}{% if a.Properties.size > 0 %}P{% endif %}{% endfor %}");
        await Assert.That(r).Contains("success=True");
    }

    [Test] public async Task FullTemplate_RendersOverCatalog()
    {
        await using var app = global::PLang.Tests.TestApp.Create("/test");
        var tmpl = "{% for a in actions %}\n"
            + "## {{ a.Name }}{% unless a.Cacheable %} [no-cache]{% endunless %}\n"
            + "{% if a.ModuleDescription or a.Description %}\n"
            + "{% if a.ModuleDescription %}{{ a.ModuleDescription }}\n{% endif %}{% if a.Description %}{{ a.Description }}\n{% endif %}{% endif %}Parameters:\n"
            + "{% if a.Properties.size > 0 %}{% for p in a.Properties %}  - {{ p.Name }}: {% if p.IsVariable %}%var%{% else %}{{ p.Type }}{% if p.Nullable %}?{% endif %}{% if p.Default %} = {{ p.Default }}{% endif %}{% endif %}\n{% endfor %}{% endif %}"
            + "{% if a.Return %}→ returns {{ a.Return }}\n{% endif %}"
            + "{% if a.ModuleNotes or a.Notes %}{% if a.ModuleNotes %}{{ a.ModuleNotes }}\n{% endif %}{% if a.Notes %}{{ a.Notes }}\n{% endif %}{% endif %}"
            + "{% if a.ModuleExamples or a.Examples %}{% if a.ModuleExamples %}{{ a.ModuleExamples }}\n{% endif %}{% if a.Examples %}{{ a.Examples }}\n{% endif %}{% endif %}{% endfor %}";
        var r = await RenderOverCatalog(app, tmpl);
        await Assert.That(r).Contains("success=True");
    }
}
