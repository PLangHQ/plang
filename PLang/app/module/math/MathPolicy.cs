using NumberPolicy = global::app.type.number.NumberPolicy;
using POverflow = global::app.type.number.OverflowMode;
using PPrecision = global::app.type.number.PrecisionMode;

namespace app.module.math;

/// <summary>
/// Resolves the per-call <see cref="NumberPolicy"/> for a math handler.
/// Precedence: step (nullable handler param) → context.Setting →
/// parent.Setting → … → App.Config.Defaults → record default
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
        var view = context.App.Config.For<global::app.module.environment.number.Config>(context);
        return new NumberPolicy
        {
            Overflow = stepOverflow ?? view.Resolve("overflow", POverflow.Promote),
            // double ⊕ decimal requires an explicit choice by default (neither
            // represents the other exactly) — matches NumberPolicy's Error default.
            // Override per-step (Precision=) or via config when a carrier is intended.
            Precision = stepPrecision ?? view.Resolve("precision", PPrecision.Error),
        };
    }
}
