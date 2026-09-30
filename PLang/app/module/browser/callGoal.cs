using app.module.browser.code;

namespace app.module.browser;

/// <summary>
/// Calls one of a page's goals — a function of the page, like <c>ShowFiles</c> — the way
/// <c>goal.call</c> calls one of plang's: <c>call ShowFiles files=%files% in %browser%</c> runs
/// <c>ShowFiles({files: …})</c> in the page, the arguments as one object, and what it returns (awaited)
/// is <c>%!data%</c>. The browser's first page, or with Window the page in that window when it is one
/// of the app's own.
/// </summary>
[Action("callGoal", Cacheable = false)]
[app.Attributes.CallsGoal(nameof(Name))]
public partial class callGoal : IContext
{
    /// <summary>The page's goal: the name of a function the page has.</summary>
    public partial data.@this<global::app.type.item.text.@this> Name { get; init; }

    /// <summary>The arguments — one named row each, as for <c>goal.call</c>; the function gets them as
    /// one object, a property per name.</summary>
    public partial data.@this<global::app.type.item.list.@this>? Parameter { get; init; }

    /// <summary>The running browser, from <c>browser.start</c>.</summary>
    public partial data.@this<Browser> Browser { get; init; }

    /// <summary>The window whose page has the goal (the id OnMessage heard as <c>"from"</c>). None: the first page.</summary>
    public partial data.@this<global::app.type.item.number.@this>? Window { get; init; }

    [Code]
    public partial IBrowser Provider { get; }

    public async Task<data.@this> Start() => await Provider.CallGoal(this);
}
