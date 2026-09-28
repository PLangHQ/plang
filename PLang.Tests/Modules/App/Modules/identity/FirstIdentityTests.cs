namespace PLang.Tests.App.Modules.identity;

// The app's first identity is made once. Its own row is signed on the way into the store, and that signing
// asks for the identity — which must answer with the one being stored, not make another.
public class FirstIdentityTests
{
    [Test] public async Task TheFirstIdentity_IsMadeOnce_ItsOwnSaveDoesntMakeAnother()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-first-id-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        try
        {
            // real signing and the real identity provider — the loop lives between them
            await using var app = new global::app.@this(root);
            var ctx = app.actor.list.User.Context;

            var gets = 0;
            foreach (var who in new[] { app.actor.list.User, app.actor.list.System })
                app.type.list["action"].Own().Bind("start", global::app.@event.When.before, (item, _, c) =>
                {
                    if (item is global::app.goal.step.action.@this { Name: "get" } a && a.Module.Name == "identity")
                        System.Threading.Interlocked.Increment(ref gets);
                    return Task.FromResult(c.Ok());
                }, who, global::app.@event.binding.Scope.actor);

            var identity = await app.Run(new global::app.module.action.identity.Get(ctx), ctx);
            await identity.IsSuccess();

            // the one ask, and the one its own save makes — never a chain down to the stack's limit
            await Assert.That(gets).IsLessThanOrEqualTo(4);
            await Assert.That(app.actor.list.System.Setting.Of<global::app.module.action.identity.setting.@this>().Identity.CountRaw).IsEqualTo(1);
        }
        finally { System.IO.Directory.Delete(root, recursive: true); }
    }
}
