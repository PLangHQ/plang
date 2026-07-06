using app.config;

namespace app.module.environment.number;

/// <summary>
/// Arithmetic policy config for <see cref="app.type.number.@this"/>.
/// Resolution: step (nullable action param) → context.Setting →
/// parent.Setting → … → App.Config.Defaults → record default.
///
/// <para>PLang prose: <c>- set environment.number.overflow = throw</c>.
/// The keys land under the <c>number</c> prefix (last namespace segment);
/// living under <c>environment</c> reflects that arithmetic policy is an
/// app-wide environment knob rather than a per-math-step concern. Both
/// keys default to the lenient values; the strict pair is one set away.</para>
/// </summary>
public partial class Config : IConfig
{
    public global::app.type.number.OverflowMode Overflow { get; set; }
        = global::app.type.number.OverflowMode.Promote;

    public global::app.type.number.PrecisionMode Precision { get; set; }
        = global::app.type.number.PrecisionMode.Double;
}
