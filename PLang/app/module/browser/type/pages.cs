using System.Text.Json;

namespace app.module.browser;

/// <summary>
/// Which DevTools page each plang-screen window shows. A window is paired with its page when it
/// gets a title: the unpaired page with that title, else the newest unpaired one. The desktop's
/// page is never a window's.
/// </summary>
internal sealed class Pages
{
    private readonly Dictionary<long, string> byWindow = new();
    private readonly HashSet<string> seen = new();
    private readonly Lock gate = new();

    /// <summary>True the first time <paramref name="page"/> is asked about.</summary>
    internal bool First(string page)
    {
        lock (gate) return seen.Add(page);
    }

    /// <summary>The desktop's page: the first one, which the browser talks to.</summary>
    internal string? Desktop { get; set; }

    /// <summary>The page window <paramref name="window"/> shows, if it is paired.</summary>
    internal string? Of(long window)
    {
        lock (gate) return byWindow.GetValueOrDefault(window);
    }

    internal void Forget(long window)
    {
        lock (gate) byWindow.Remove(window);
    }

    /// <summary>Window <paramref name="window"/>'s page in <paramref name="list"/> (DevTools' /json/list,
    /// newest first), pairing it first if it has none.</summary>
    internal JsonElement? Match(long window, string title, JsonElement list)
    {
        lock (gate)
        {
            var pages = list.EnumerateArray()
                .Where(t => t.GetProperty("type").GetString() == "page" && t.GetProperty("id").GetString() != Desktop)
                .ToList();
            if (byWindow.TryGetValue(window, out var known))
                return pages.Where(p => p.GetProperty("id").GetString() == known).Select(p => (JsonElement?)p).FirstOrDefault();
            var taken = byWindow.Values.ToHashSet();
            var free = pages.Where(p => !taken.Contains(p.GetProperty("id").GetString()!)).ToList();
            var match = free.Where(p => p.GetProperty("title").GetString() == title).Select(p => (JsonElement?)p).FirstOrDefault()
                ?? free.Select(p => (JsonElement?)p).FirstOrDefault();
            if (match is { } page) byWindow[window] = page.GetProperty("id").GetString()!;
            return match;
        }
    }
}
