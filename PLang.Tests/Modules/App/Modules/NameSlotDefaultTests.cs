namespace PLang.Tests.App.Modules;

// Every action's name slot with a [Default] (loop.foreach's Item, `item`) resolves from a .pr that holds no row for
// it — the shape the builder writes when the step leaves the default — to the variable its default names. Walked
// over the whole catalog, so the next name slot with a default is held to it too.
public class NameSlotDefaultTests
{
    [Test]
    public async Task EveryNameSlotsDefault_ResolvesWithoutItsRow()
    {
        await using var app = new global::app.@this("/app").Testing();
        var context = app.actor.list.User.Context;
        var checkedSlots = new List<string>();
        var failed = new List<string>();

        foreach (var module in app.module.list.Items())
            foreach (var name in module.ActionNames.ToList())
            {
                var element = module[name]!;
                foreach (var slot in element.Property.Where(p => p.Type.Name == "variable" && p.HasDefault))
                {
                    var where = $"{module.Name}.{name}.{slot.Name}";
                    checkedSlots.Add(where);
                    var (handler, error) = await new PrAction { Module = module, Name = name, Synthetic = false }.Bind(context);
                    if (handler == null) { failed.Add($"{where}: {error?.Message}"); continue; }
                    var held = (global::app.data.@this<global::app.type.item.variable.@this>?)handler.GetType().GetProperty(slot.Name)!.GetValue(handler);
                    var variable = held == null ? null : await held.Value();
                    if (held is not { Success: true } || variable?.Name != slot.Default?.ToString())
                        failed.Add($"{where}: {held?.Error?.Message ?? variable?.Name ?? "nothing"}");
                }
            }

        await Assert.That(checkedSlots).Contains("loop.foreach.Item");
        await Assert.That(failed).IsEmpty();
    }

    // a default the build froze and the one a run falls back on are born the same way: the same Item either way
    [Test]
    public async Task AFrozenDefault_AndNone_GiveTheSameItem()
    {
        await using var app = new global::app.@this("/app").Testing();
        var context = app.actor.list.User.Context;
        var loop = app.module.list.Items().First(m => m.Name == "loop");
        async Task<global::app.type.item.variable.@this?> Item(bool frozen)
        {
            var action = new PrAction { Module = loop, Name = "foreach", Synthetic = false };
            if (frozen)
            {
                action.Freeze(context);
                if (action.Default["item"] == null) throw new System.InvalidOperationException("the build froze no default for Item");
            }
            var (handler, _) = await action.Bind(context);
            return await ((global::app.module.loop.Foreach)handler!).Item!.Value();
        }

        var withFrozen = await Item(frozen: true);
        var without = await Item(frozen: false);

        await Assert.That(withFrozen?.Name).IsEqualTo("item");
        await Assert.That(without?.Name).IsEqualTo(withFrozen?.Name);
        await Assert.That(without?.Text).IsEqualTo(withFrozen?.Text);
    }
}
