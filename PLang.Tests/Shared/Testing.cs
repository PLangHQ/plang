namespace PLang.Tests;

/// <summary>
/// A test's app, composed only from production doors — nothing a program's app can't do, and no state of its
/// own. <see cref="Testing"/> opens a test session (<c>app.test.list.Open()</c>: test mode, the store in memory,
/// scoped by the app's id) and swaps in the no-crypto signing mock through the code door; a test that exercises
/// real crypto leaves it out.
/// </summary>
public static class AppTesting
{
    /// <summary>This app as a test runs it: a test session open, signing through the no-crypto mock.</summary>
    public static global::app.@this Testing(this global::app.@this app)
    {
        app.test.list.Open();
        return app.TestSigning();
    }

    /// <summary>This app building, as <c>plang build</c> starts it: born with its Build — the goals' steps are
    /// checked over its files (<c>app.Build.Files</c>), where a test adds a mock.</summary>
    public static global::app.@this Building(this global::app.@this app)
    {
        app.Build = new global::app.module.build.@this(app.actor.list.System.Context);
        return app;
    }

    /// <summary>
    /// Signing through the no-crypto <see cref="global::PLang.Tests.Shared.TestSigning"/> mock, through the code
    /// door — for an app kept on disk (its own store) that doesn't test crypto. Real ed25519 keygen + keccak256 +
    /// signing per Data dominates wall-clock and, run massively in parallel, makes suites look hung. The mock
    /// replaces both ISigning and IKey (keygen); IsBuiltIn keeps it out of the Code snapshot.
    /// </summary>
    public static global::app.@this TestSigning(this global::app.@this app)
    {
        var provider = new global::PLang.Tests.Shared.TestSigning { IsBuiltIn = true };
        app.Code.Register<global::app.module.signing.code.ISigning>(provider);
        app.Code.Register<global::app.module.signing.code.IKey>(provider);
        app.Code.SetDefault<global::app.module.signing.code.ISigning>("test-signing");
        app.Code.SetDefault<global::app.module.signing.code.IKey>("test-signing");
        return app;
    }

    /// <summary>The action <paramref name="formal"/> reads as (<c>file.read(Path="a.txt")</c>) — built as the
    /// builder builds one: the step notation's reader, over this app's modules, for a step of its own. Its
    /// <c>Start(context)</c> runs it as plang does. A line the reader refuses is the test's own mistake: thrown.</summary>
    public static global::app.goal.step.action.@this Action(this global::app.actor.context.@this context, string formal)
    {
        var step = new global::app.goal.step.@this { Text = formal };
        var read = new global::app.goal.step.action.formal.Reader(step, context.App.module.list).Read(formal, context);
        if (!read.Success) throw new System.InvalidOperationException($"'{formal}' does not read: {read.Error!.Message}");
        return ((global::app.goal.step.action.list.@this)read.Peek())[0];
    }

    /// <summary>The in-memory <see cref="global::PLang.Tests.Shared.TestIdentity"/>, through the code door — for
    /// real-signing fixtures that need an identity to sign with but don't test the identity provider.</summary>
    public static global::app.@this TestIdentity(this global::app.@this app)
    {
        app.Code.Register<global::app.module.identity.code.IIdentity>(new global::PLang.Tests.Shared.TestIdentity { IsBuiltIn = true });
        app.Code.SetDefault<global::app.module.identity.code.IIdentity>("test-identity");
        return app;
    }
}
