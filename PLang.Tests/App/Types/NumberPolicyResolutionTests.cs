using POverflow = global::app.types.number.OverflowMode;
using PPrecision = global::app.types.number.PrecisionMode;
using PPolicy = global::app.types.number.NumberPolicy;

namespace PLang.Tests.App.Types;

// plang-types — Stage 4
// Policy resolves through app.config: step (nullable action param) → context (ConfigScope)
// → parent contexts → App.Config.Defaults → record-default. Goal is NOT a policy carrier.
// Defaults are lenient (Promote, Double); strict (Throw, Decimal) is one set away.

public class NumberPolicyResolutionTests
{
    private static global::app.@this NewApp()
        => new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-policy-" + System.Guid.NewGuid().ToString("N")[..8]));

    [Test] public async Task Resolve_StepLevel_OverridesContext()
    {
        await using var app = NewApp();
        var ctx = app.User.Context;
        // Context says Decimal; step override says Double.
        app.Config.Set("number.precision", PPrecision.Decimal, ctx);
        var p = global::app.modules.math.MathPolicy.Resolve(ctx, stepOverflow: null, stepPrecision: PPrecision.Double);
        await Assert.That(p.Precision).IsEqualTo(PPrecision.Double);
    }

    [Test] public async Task Resolve_ContextLevel_OverridesAppDefault()
    {
        await using var app = NewApp();
        var ctx = app.User.Context;
        app.Config.Set("number.overflow", POverflow.Throw, ctx, isDefault: true);   // app default
        app.Config.Set("number.overflow", POverflow.Promote, ctx);                  // context override
        var p = global::app.modules.math.MathPolicy.Resolve(ctx, null, null);
        await Assert.That(p.Overflow).IsEqualTo(POverflow.Promote);
    }

    [Test] public async Task Resolve_AppDefault_FromAppConfigDefaults()
    {
        await using var app = NewApp();
        var ctx = app.User.Context;
        app.Config.Set("number.overflow", POverflow.Throw, ctx, isDefault: true);
        var p = global::app.modules.math.MathPolicy.Resolve(ctx, null, null);
        await Assert.That(p.Overflow).IsEqualTo(POverflow.Throw);
    }

    [Test] public async Task Resolve_RecordDefault_LenientPromoteDouble_WhenNothingSet()
    {
        await using var app = NewApp();
        var p = global::app.modules.math.MathPolicy.Resolve(app.User.Context, null, null);
        await Assert.That(p.Overflow).IsEqualTo(POverflow.Promote);
        await Assert.That(p.Precision).IsEqualTo(PPrecision.Double);
    }

    [Test] public async Task Resolve_SubContext_ClimbsParent_InheritsParentSetting()
    {
        // Parent context sets number.overflow=Throw; child context inherits via
        // the ConfigScope.Resolve walk (this → Parent → App.Config.Defaults).
        await using var app = NewApp();
        var parent = app.User.Context;
        app.Config.Set("number.overflow", POverflow.Throw, parent);

        var child = new global::app.actor.context.@this(app, parent: parent);
        var p = global::app.modules.math.MathPolicy.Resolve(child, null, null);
        await Assert.That(p.Overflow).IsEqualTo(POverflow.Throw);
    }

    [Test] public async Task NumberPolicy_IsReadonlyStruct_OverflowAndPrecisionAxes()
    {
        var t = typeof(PPolicy);
        await Assert.That(t.IsValueType).IsTrue();
        await Assert.That(t.GetProperty("Overflow")).IsNotNull();
        await Assert.That(t.GetProperty("Precision")).IsNotNull();
    }
}
