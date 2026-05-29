using app.config;

namespace app.modules.math.number;

/// <summary>
/// Arithmetic policy config for <see cref="app.types.number.@this"/>.
/// Resolution: step (nullable action param) → context.ConfigScope →
/// parent.ConfigScope → … → App.Config.Defaults → record default.
///
/// <para>PLang prose: <c>- set math.number.overflow = throw</c>. Both
/// keys default to the lenient values; the strict pair is one set away.</para>
/// </summary>
public partial class Config : IConfig
{
    public global::app.types.number.OverflowMode Overflow { get; set; }
        = global::app.types.number.OverflowMode.Promote;

    public global::app.types.number.PrecisionMode Precision { get; set; }
        = global::app.types.number.PrecisionMode.Double;
}
