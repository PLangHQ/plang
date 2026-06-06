using app.variable;
using app.module.ui.code;

namespace app.module.ui;

[Action("render")]
public partial class Render : IContext
{
    /// <summary>Template content (inline Liquid) or file path. Determined by IsFile.</summary>
    [IsNotNull]
    public partial data.@this<global::app.type.text.@this> Template { get; init; }

    /// <summary>Explicit parameters that override Variables in the template.</summary>
    public partial data.@this<global::app.type.list.@this>? Parameters { get; init; }

    /// <summary>
    /// Force file/inline interpretation.
    /// true = treat Template as a file path (error if not found).
    /// false = treat Template as inline content.
    /// null (default) = auto-detect via file existence check.
    /// </summary>
    public partial data.@this<global::app.type.@bool.@this>? IsFile { get; init; }

    [Code]
    public partial ITemplate Provider { get; }

    public async Task<data.@this<global::app.type.text.@this>> Run() => await Provider.Render(this);
}
