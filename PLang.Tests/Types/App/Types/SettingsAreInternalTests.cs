namespace PLang.Tests.App.Types;

// Decision 774: a settings class is a type (navigable as `%!x.setting!type%`), but a settings SUBTYPE is
// never LLM vocabulary — it must be Internal, so it stays out of the type offerings (the decider's
// criteria), the catalog, and the surface's face. The base `setting` type itself stays offered (a value
// can be typed `setting`); every subtype — a module/action base or a concrete class — must say
// `public static bool Internal => true`. This pin fails the moment one forgets, before it leaks quietly.
public class SettingsAreInternalTests
{
    [Test]
    public async Task EverySettingsSubtype_IsInternal()
    {
        await using var app = global::PLang.Tests.Session.App(
            "/tmp/plang_settings_" + System.Guid.NewGuid().ToString("N")[..8]).Testing();
        var leaked = app.type.list.Items()
            .Where(t => t.ClrType is { } clr
                        && typeof(global::app.type.item.setting.@this).IsAssignableFrom(clr)
                        && clr != typeof(global::app.type.item.setting.@this))
            .Where(t => !t.Internal)
            .Select(t => t.Name)
            .OrderBy(n => n, System.StringComparer.Ordinal)
            .ToList();
        await Assert.That(string.Join("\n", leaked)).IsEqualTo("");
    }
}
