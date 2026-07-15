using app.module.ui;
using app.module.ui.code;
using Op = global::app.module.condition.Operator;
using Where = global::app.module.list.Where;
using ItemList = global::app.type.item.list.@this;

namespace PLang.Tests.App.Modules.Stage4Spike;

// Stage 4 five-leg spike — de-risk the risky mechanics BEFORE the 4a collection
// split lands. Renders REAL host-element shapes through the REAL Fluid provider
// and runs the REAL list.where. Spike POCOs below are throwaway test-locals that
// mirror the intended element/action/property shapes (Name, Actions native list,
// prose doors, property rows) — no production shape changes in this commit.
//
// Legs (architect plan.md §SPIKE):
//   (a) enumerate host elements in a template
//   (b) Fluid filters (where:/map:) over element properties on a native list
//   (c) the property-row host
//   (d) async prose doors — least proven (methods vs sync props vs Task-props)
//   (e) list.where subject.Get(field) over clr(action)
public class HostRenderSpikeTests
{
    // --- Spike shapes (mirror the intended 4a elements; test-local only) ---

    private sealed class SpikeProperty
    {
        public required string Name { get; init; }
        public required string TypeName { get; init; }
        public bool IsVariable { get; init; }
        public bool Nullable { get; init; }
        public object? Default { get; init; }
    }

    private sealed class SpikeAction
    {
        public required string Name { get; init; }          // "file.read"
        public required string ActionName { get; init; }
        public required ItemList Properties { get; init; }
    }

    private sealed class SpikeModule
    {
        public required string Name { get; init; }
        public required ItemList Actions { get; init; }

        // Leg (d) — the SAME prose in three exposure forms, to measure what Fluid
        // can actually read:
        public string DescriptionSync { get; init; } = "";                              // sync property
        public Task<string?> DescriptionTaskProp => Task.FromResult<string?>(_prose);    // Task-valued property
        public Task<string?> DescriptionMethod() => Task.FromResult<string?>(_prose);    // async-style method (draft's door)
        private readonly string _prose;
        public SpikeModule(string prose) { _prose = prose; DescriptionSync = prose; }
    }

    private static ItemList NativeList(global::app.actor.context.@this ctx, params object?[] elems)
        => new(new List<object?>(elems), ctx);

    private static ItemList SampleModules(global::app.actor.context.@this ctx)
    {
        SpikeProperty P(string n, string t, bool v = false, bool nul = false, object? def = null)
            => new() { Name = n, TypeName = t, IsVariable = v, Nullable = nul, Default = def };

        var fileRead = new SpikeAction
        {
            Name = "file.read", ActionName = "read",
            Properties = NativeList(ctx, P("Path", "path"), P("Encoding", "string", nul: true, def: "utf-8")),
        };
        var varSet = new SpikeAction
        {
            Name = "variable.set", ActionName = "set",
            Properties = NativeList(ctx, P("Name", "string", v: true), P("Value", "object")),
        };
        var fileMod = new SpikeModule("Read and write files.") { Name = "file", Actions = NativeList(ctx, fileRead) };
        var varMod = new SpikeModule("") { Name = "variable", Actions = NativeList(ctx, varSet) };  // no prose
        return NativeList(ctx, fileMod, varMod);
    }

    // Bind the catalog as a VARIABLE (%modules%) — the builder's own path
    // (`get all modules → %modules%`, then render). The Parameters slot does not
    // bind a native list; a variable does.
    private static async Task<string> Render(global::app.@this app, string template, ItemList modules)
    {
        var ctx = app.User.Context;
        ctx.Variable.Set(new Data("modules", modules, context: ctx));
        var action = new Render(ctx)
        {
            Template = (global::app.type.item.text.@this)template,
            IsFile = (global::app.type.item.@bool.@this)false,
        };
        var result = await new global::app.module.ui.code.Fluid().Render(action);
        await result.IsSuccess();
        return (await result.Value())?.ToString() ?? "";
    }

    // --- Leg (a): enumerate host elements ---
    [Test]
    public async Task LegA_EnumerateHostElements()
    {
        var app = global::PLang.Tests.TestApp.Plain("/tmp/s4spike-a");
        var modules = SampleModules(app.User.Context);
        var outp = await Render(app, "{% for m in modules %}[{{ m.Name }}]{% endfor %}", modules);
        await Assert.That(outp).IsEqualTo("[file][variable]");
    }

    // --- Leg (b): Fluid filters over element properties on a native list ---
    [Test]
    public async Task LegB_FluidFilterOverElements()
    {
        var app = global::PLang.Tests.TestApp.Plain("/tmp/s4spike-b");
        var modules = SampleModules(app.User.Context);
        // where: filter on element property Name, then map: to collect names.
        var outp = await Render(app,
            "{% assign f = modules | where: 'Name', 'file' %}{{ f | map: 'Name' | join: ',' }}", modules);
        await Assert.That(outp).IsEqualTo("file");
    }

    // --- Leg (c): the property-row host ---
    [Test]
    public async Task LegC_PropertyRowHost()
    {
        var app = global::PLang.Tests.TestApp.Plain("/tmp/s4spike-c");
        var modules = SampleModules(app.User.Context);
        var outp = await Render(app,
            "{% for m in modules %}{% for a in m.Actions %}{% for p in a.Properties %}" +
            "{{ p.Name }}:{% if p.IsVariable %}%var%{% else %}{{ p.TypeName }}{% if p.Nullable %}?{% endif %}{% endif %}" +
            "{% if p.Default %}={{ p.Default }}{% endif %} {% endfor %}{% endfor %}{% endfor %}",
            modules);
        // file.read: Path:path Encoding:string?=utf-8 ; variable.set: Name:%var% Value:object
        await Assert.That(outp).IsEqualTo("Path:path Encoding:string?=utf-8 Name:%var% Value:object ");
    }

    // --- Leg (d): prose doors — which exposure form does Fluid actually read? ---
    // Finding: a SYNC property reads through the door; a method is unreachable (Liquid never
    // invokes methods) and a Task-valued property does not resolve to its awaited value. So an
    // element's prose must be a SYNC property (resolved/cached at mint), not an async method.
    [Test]
    public async Task LegD_ProseDoorMustBeSyncProperty()
    {
        var app = global::PLang.Tests.TestApp.Plain("/tmp/s4spike-d");
        var modules = SampleModules(app.User.Context);
        var syncOut = await Render(app, "{% for m in modules %}[{{ m.DescriptionSync }}]{% endfor %}", modules);
        var methodOut = await Render(app, "{% for m in modules %}[{{ m.DescriptionMethod }}]{% endfor %}", modules);

        await Assert.That(syncOut).IsEqualTo("[Read and write files.][]");       // sync property — reads
        await Assert.That(methodOut).IsEqualTo("[][]");                          // method — unreachable
    }

    // --- Leg (e): list.where subject.Get(field) over clr(action) ---
    [Test]
    public async Task LegE_WhereOverClrAction()
    {
        var app = global::PLang.Tests.TestApp.Create("/tmp/s4spike-e");
        var ctx = app.User.Context;

        // Real catalog action elements ride as raw POCOs (clr) in a native list —
        // the shape 4a's app.module surface will answer.
        var catalog = await app.Module.Describe();
        var subset = catalog.Where(a => a.Module == "file" || a.Module == "variable").ToList();
        var actions = new ItemList(new List<object?>(subset.Cast<object?>()), ctx);
        ctx.Variable.Set(new Data("actions", actions, context: ctx));

        // where %actions% ActionName in ["read","set"]  — proves Get(field) over clr(action)
        // + the "in" operator, the exact mechanic behind `where %actions% Name in %planStep.actions%`.
        var wanted = new ItemList(new List<object?> { "read", "set" }, ctx);
        var where = new Where(ctx)
        {
            ListName = new global::app.variable.@this("actions"),
            Field = new global::app.data.@this<global::app.type.item.text.@this>("", "ActionName", context: ctx),
            Operator = new global::app.data.@this<global::app.type.item.choice.@this<Op>>("", new Op("in"), context: ctx),
            Value = new Data("", wanted, context: ctx),
        };
        var result = await where.Run();
        await result.IsSuccess();

        var kept = (await result.Value()) as ItemList;
        await Assert.That(kept).IsNotNull();

        // Read each kept element's ActionName through the SAME value door `where` used
        // (`d.Get(field)`) — dogfoods the navigation under test, no reflecting helper.
        var names = new List<string>();
        foreach (var d in kept!.Items)
            names.Add((await d.Get("ActionName"))?.Peek()?.ToString() ?? "");

        foreach (var n in names)
            await Assert.That(new[] { "read", "set" }).Contains(n);
        await Assert.That(names.Count).IsGreaterThan(0);
        await Assert.That(names.Count).IsLessThan(subset.Count);
    }
}
