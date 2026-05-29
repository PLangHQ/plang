using NumberPolicy = global::app.type.number.NumberPolicy;
using POverflow = global::app.type.number.OverflowMode;
using PPrecision = global::app.type.number.PrecisionMode;

namespace app.modules.math;

/// <summary>
/// Resolves the per-call <see cref="NumberPolicy"/> for a math handler.
/// Precedence: step (nullable handler param) → context.ConfigScope →
/// parent.ConfigScope → … → App.Config.Defaults → record default
/// (Promote / Double).
///
/// <para>The handler passes its nullable step overrides; everything else
/// flows through <see cref="app.config.@this.For{T}"/>'s view.</para>
/// </summary>
internal static class MathPolicy
{
    public static NumberPolicy Resolve(actor.context.@this context,
        POverflow? stepOverflow, PPrecision? stepPrecision)
    {
        var view = context.App.Config.For<global::app.modules.environment.number.Config>(context);
        return new NumberPolicy
        {
            Overflow = stepOverflow ?? view.Resolve("overflow", POverflow.Promote),
            Precision = stepPrecision ?? view.Resolve("precision", PPrecision.Double),
        };
    }
}
