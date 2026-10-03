using System.Text.Json;

namespace PLang.Tests.App.Modules.browser;

/// <summary>
/// The goals that ship with plang and start Chromium (<c>/system/browser/</c>) start it clean: none of plang's
/// environment (its keys, any token the user's shell holds) reaches the browser, only what each names — the display's
/// two variables, and what it keeps of plang's by name (sound, home). Read off the built .pr, as plang runs it.
/// </summary>
public class ShippedGoalTests
{
    private static IEnumerable<JsonElement> Starts(string goal)
    {
        var pr = Path.Combine(AppContext.BaseDirectory, "os", "system", "browser", ".build", goal + ".pr");
        using var doc = JsonDocument.Parse(File.ReadAllText(pr));
        return Walk(doc.RootElement).Where(e => e.TryGetProperty("module", out var m) && m.GetString() == "terminal").Select(e => e.Clone()).ToList();
    }

    private static IEnumerable<JsonElement> Walk(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Object)
        {
            yield return e;
            foreach (var p in e.EnumerateObject()) foreach (var inner in Walk(p.Value)) yield return inner;
        }
        else if (e.ValueKind == JsonValueKind.Array)
            foreach (var item in e.EnumerateArray()) foreach (var inner in Walk(item)) yield return inner;
    }

    private static JsonElement? Property(JsonElement start, string name)
        => start.GetProperty("property").EnumerateArray().Where(p => p.GetProperty("name").GetString() == name)
            .Select(p => (JsonElement?)p.GetProperty("value")).FirstOrDefault();

    [Test]
    [Arguments("screen", "PULSE_SERVER,HOME", "XDG_RUNTIME_DIR,WAYLAND_DISPLAY")]
    [Arguments("window", "PULSE_SERVER,HOME", "XDG_RUNTIME_DIR,WAYLAND_DISPLAY")]
    [Arguments("headless", "HOME", "")]
    public async Task EveryChromiumStart_IsClean_KeepingOnlyWhatItNames(string goal, string keeps, string sets)
    {
        var starts = Starts(goal).ToList();
        await Assert.That(starts.Count).IsEqualTo(1);
        var start = starts[0];
        await Assert.That(Property(start, "Clean")?.GetBoolean()).IsTrue().Because($"{goal} starts Chromium with none of plang's environment");
        var kept = Property(start, "Keep")?.EnumerateArray().Select(k => k.GetString()).ToList() ?? [];
        await Assert.That(string.Join(",", kept)).IsEqualTo(keeps);
        var set = Property(start, "Environment") is { ValueKind: JsonValueKind.Object } env
            ? string.Join(",", env.EnumerateObject().Select(p => p.Name)) : "";
        await Assert.That(set).IsEqualTo(sets);
    }
}
