using app.module.code;

namespace app.module.ui.code;

/// <summary>
/// Template rendering provider. Swappable via app.Code.
/// Default implementation uses Fluid (Liquid syntax).
/// </summary>
public interface ITemplate : ICode
{
    /// <summary>Renders the template and hands the rendered characters to the declared format's own birth
    /// (the format's Decode): a formatted template (.json) is born an unread item of that kind ({item, json}),
    /// a plain/inline one a text. Bare Data — the value is the format's birth, produced there.</summary>
    Task<data.@this> Render(Render action);
}
