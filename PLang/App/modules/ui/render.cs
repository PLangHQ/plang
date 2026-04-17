using App.Variables;
using App.modules.ui.providers;

namespace App.modules.ui;

[Example("render 'email.html', write to %body%", "Template=email.html")]
[Example("render 'page.html' with title=%pageTitle%, write to %html%", "Template=page.html, Parameters={title: %pageTitle%}")]
[Example("render %templateContent%, write to %result%", "Template=%templateContent%")]
[Example("render file 'report.html', write to %output%", "Template=report.html, IsFile=true")]
[Action("render")]
public partial class Render : IContext
{
    /// <summary>Template content (inline Liquid) or file path. Determined by IsFile.</summary>
    [IsNotNull]
    public partial Data.@this<string> Template { get; init; }

    /// <summary>Explicit parameters that override Variables in the template.</summary>
    public partial Data.@this<List<Data.@this>>? Parameters { get; init; }

    /// <summary>
    /// Force file/inline interpretation.
    /// true = treat Template as a file path (error if not found).
    /// false = treat Template as inline content.
    /// null (default) = auto-detect via file existence check.
    /// </summary>
    public partial Data.@this<bool>? IsFile { get; init; }

    [Provider]
    public partial ITemplateProvider Provider { get; }

    public async Task<Data.@this> Run() => await Provider.Render(this);
}
