using POverflow = global::app.type.number.OverflowMode;
using PPrecision = global::app.type.number.PrecisionMode;
using PPolicy = global::app.type.number.NumberPolicy;

namespace PLang.Tests.App.Types;

// plang-types — Stage 4
// Policy resolves through app.config: step (nullable action param) → context (ConfigScope)
// → parent contexts → App.Config.Defaults → record-default. Goal is NOT a policy carrier.
// Defaults are lenient (Promote, Double); strict (Throw, Decimal) is one set away.

public class NumberPolicyResolutionTests
{
    private static global::app.@this NewApp()
        => TestApp.Create(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-policy-" + System.Guid.NewGuid().ToString("N")[..8]));

    [Test] public async Task Resolve_StepLevel_OverridesContext()
    {
        await using var app = NewApp();
        var context = app.User.Context;
        // Context says Decimal; step override says Double.
        app.Config.Set("number.precision", PPrecision.Decimal, context);
        var p = global::app.module.math.MathPolicy.Resolve(context, stepOverflow: null, stepPrecision: PPrecision.Double);
        await Assert.That(p.Precision).IsEqualTo(PPrecision.Double);
    }

    [Test] public async Task Resolve_ContextLevel_OverridesAppDefault()
    {
        await using var app = NewApp();
        var context = app.User.Context;
        app.Config.Set("number.overflow", POverflow.Throw, context, isDefault: true);   // app default
        app.Config.Set("number.overflow", POverflow.Promote, context);                  // context override
        var p = global::app.module.math.MathPolicy.Resolve(context, null, null);
        await Assert.That(p.Overflow).IsEqualTo(POverflow.Promote);
    }

    [Test] public async Task Resolve_AppDefault_FromAppConfigDefaults()
    {
        await using var app = NewApp();
        var context = app.User.Context;
        app.Config.Set("number.overflow", POverflow.Throw, context, isDefault: true);
        var p = global::app.module.math.MathPolicy.Resolve(context, null, null);
        await Assert.That(p.Overflow).IsEqualTo(POverflow.Throw);
    }

    [Test] public async Task Resolve_RecordDefault_PromoteAndPrecisionError_WhenNothingSet()
    {
        await using var app = NewApp();
        var p = global::app.module.math.MathPolicy.Resolve(app.User.Context, null, null);
        await Assert.That(p.Overflow).IsEqualTo(POverflow.Promote);
        // Precision defaults to Error: double ⊕ decimal demands an explicit choice.
        await Assert.That(p.Precision).IsEqualTo(PPrecision.Error);
    }

    [Test] public async Task Resolve_SubContext_ClimbsParent_InheritsParentSetting()
    {
        // Parent context sets number.overflow=Throw; child context inherits via
        // the ConfigScope.Resolve walk (this → Parent → App.Config.Defaults).
        await using var app = NewApp();
        var parent = app.User.Context;
        app.Config.Set("number.overflow", POverflow.Throw, parent);

        var child = new global::app.actor.context.@this(app, app.User, parent: parent);
        var p = global::app.module.math.MathPolicy.Resolve(child, null, null);
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
