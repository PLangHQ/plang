using app.variables;
using app.modules.ui.code;

namespace app.modules.ui;

[ModuleDescription("Render Liquid templates from inline content or files with PLang variable injection")]
[System.ComponentModel.Description("Render a Liquid template (file or inline) with the current variables and optional explicit parameters")]
[Example("render 'page.html' with title=%pageTitle%, write to %html%",
    "ui.render Template([string] page.html), Parameters([list<object>] [{\"Name\":\"title\",\"Value\":\"%pageTitle%\"}]) | variable.set Name([string] %html%), Value([object] %__data__%)")]
[Action("render")]
public partial class Render : IContext
{
    /// <summary>Template content (inline Liquid) or file path. Determined by IsFile.</summary>
    [IsNotNull]
    public partial data.@this<string> Template { get; init; }

    /// <summary>Explicit parameters that override Variables in the template.</summary>
    public partial data.@this<List<data.@this>>? Parameters { get; init; }

    /// <summary>
    /// Force file/inline interpretation.
    /// true = treat Template as a file path (error if not found).
    /// false = treat Template as inline content.
    /// null (default) = auto-detect via file existence check.
    /// </summary>
    public partial data.@this<bool>? IsFile { get; init; }

    [Code]
    public partial ITemplate Provider { get; }

    public async Task<data.@this> Run() => await Provider.Render(this);
}
