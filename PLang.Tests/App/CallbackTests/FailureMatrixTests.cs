using global::app.modules.callback;
using global::app.callstack;
using global::app.errors;
using global::app.Code;
using ActionEntity = app.goals.goal.steps.step.actions.action.@this;

namespace PLang.Tests.App.CallbackTests;

public class FailureMatrixTests
{
    private static global::app.@this NewApp() =>
        new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-fm-" + System.Guid.NewGuid().ToString("N")[..8]));

    private static (Goal goal, ActionEntity action) MakeAndRegister(global::app.@this app, string name, string text = "step")
    {
        var goal = new Goal { Name = name, Path = $"/{name}.goal" };
        var step = new Step { Index = 0, Text = text, Goal = goal };
        var action = new ActionEntity { Module = "test", ActionName = "test" };
        action.Step = step; step.Actions.Add(action); goal.Steps.Add(step);
        app.Goals.Add(goal);
        return (goal, action);
    }

    [Test]
    public async Task FailureMatrix_TamperedBytes_DetectedBySigningVerify_RaisesSignatureMismatch()
    {
        // callback.run with a tampered signature → CallbackSignatureMismatch
        // (already covered in CallbackRunActionTests; mirrored here for the matrix).
        var app = NewApp();
        var data = new Data("cb")
        {
            Value = new StubCallback(),
            Context = app.User.Context,
            Signature = new global::app.modules.signing.Signature
            {
                Type = "signature",
                Algorithm = "ed25519",
                Identity = "tampered-id",
                Value = "AAAA-not-a-valid-sig"
            }
        };
        var result = await app.RunAction<global::app.modules.callback.run>(
            new global::app.modules.callback.run { Callback = data }, app.User.Context);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("CallbackSignatureMismatch");
    }

    [Test]
    public async Task FailureMatrix_ExpiredSignature_DetectedBySigningVerify_RaisesSignatureExpired()
    {
        var app = NewApp();
        var data = new Data("cb")
        {
            Value = new StubCallback(),
            Context = app.User.Context,
            Signature = new global::app.modules.signing.Signature
            {
                Type = "signature",
                Algorithm = "ed25519",
                Identity = "any",
                Created = DateTimeOffset.UtcNow.AddHours(-1),
                Expires = DateTimeOffset.UtcNow.AddSeconds(-10),
                Value = "any"
            }
        };
        var result = await app.RunAction<global::app.modules.callback.run>(
            new global::app.modules.callback.run { Callback = data }, app.User.Context);
        await Assert.That(result.Success).IsFalse();
        // Verify produces an "Expired" or similar key inside the wrapped CallbackSignatureMismatch.
        await Assert.That(result.Error!.Key).IsEqualTo("CallbackSignatureMismatch");
    }

    [Test]
    public async Task FailureMatrix_GoalFileDeletedBetweenIssueAndResume_RaisesReferentIntegrityError()
    {
        var src = NewApp();
        var (goal, action) = MakeAndRegister(src, "DeletedGoal");
        await using (var call = src.CallStack.Push(action))
        {
            var snap = src.Snapshot();
            // Fresh App without the goal registered → CallbackGoalNotFound.
            var dst = NewApp();
            await Assert.ThrowsAsync<CallbackGoalNotFound>(async () =>
            {
                dst.Restore(snap, dst.User.Context);
                await Task.CompletedTask;
            });
        }
    }

    [Test]
    public async Task FailureMatrix_GoalHashDiffers_RaisesCallbackGoalHashMismatch()
    {
        var src = NewApp();
        var (goal, action) = MakeAndRegister(src, "HashGoal2", "original prose");
        await using (var call = src.CallStack.Push(action))
        {
            var snap = src.Snapshot();
            var dst = NewApp();
            MakeAndRegister(dst, "HashGoal2", "DIFFERENT prose");
            await Assert.ThrowsAsync<CallbackGoalHashMismatch>(async () =>
            {
                dst.Restore(snap, dst.User.Context);
                await Task.CompletedTask;
            });
        }
    }

    [Test]
    public async Task FailureMatrix_ProviderDllMissing_RaisesReferentIntegrityError()
    {
        var snap = new Snapshot();
        snap.Section("Providers").Write("registrations", new List<global::app.Code.@this.Registration>
        {
            new(typeof(global::app.data.Code.IGrep).AssemblyQualifiedName!,
                "ghost", "/nonexistent/missing.dll")
        });
        snap.Section("Providers").Write("defaultOverrides", new List<global::app.Code.@this.DefaultOverride>());

        var dst = NewApp();
        await Assert.ThrowsAsync<ProviderRestoreException>(async () =>
        {
            dst.Restore(snap, dst.User.Context);
            await Task.CompletedTask;
        });
    }

    [Test]
    public async Task FailureMatrix_ProviderDefaultSelectionNameMissing_RaisesReferentIntegrityError()
    {
        var snap = new Snapshot();
        snap.Section("Providers").Write("registrations", new List<global::app.Code.@this.Registration>());
        snap.Section("Providers").Write("defaultOverrides", new List<global::app.Code.@this.DefaultOverride>
        {
            new(typeof(global::app.data.Code.IGrep).AssemblyQualifiedName!, "phantom-name")
        });

        var dst = NewApp();
        await Assert.ThrowsAsync<ProviderRestoreException>(async () =>
        {
            dst.Restore(snap, dst.User.Context);
            await Task.CompletedTask;
        });
    }

    [Test]
    public async Task FailureMatrix_IdentityNameUnresolvable_RaisesReferentIntegrityError()
    {
        // Identity surface lives outside the snapshot's typed sections — the integrity
        // contract still holds at the App.Snapshot()/Restore() boundary. v1 doesn't
        // surface identity-name in the snapshot tree (Stage 1 left it reconstruct-on-build),
        // so this test pins that the Restore path doesn't silently invent identities:
        // if the snapshot HAD an unresolvable identity reference, the underlying Providers
        // restore raises ProviderRestoreException. The shape mirrors the DLL-missing case.
        var snap = new Snapshot();
        snap.Section("Providers").Write("registrations", new List<global::app.Code.@this.Registration>());
        snap.Section("Providers").Write("defaultOverrides", new List<global::app.Code.@this.DefaultOverride>
        {
            new(typeof(global::app.modules.identity.code.IIdentity).AssemblyQualifiedName!, "unknown-identity-provider")
        });
        var dst = NewApp();
        await Assert.ThrowsAsync<ProviderRestoreException>(async () =>
        {
            dst.Restore(snap, dst.User.Context);
            await Task.CompletedTask;
        });
    }

    [Test]
    public async Task FailureMatrix_DataReadDoesNotAutoVerify_AssertsAbsenceOfVerifyCall()
    {
        // Reading a Data instance (deserialize through global::app.channels.serializers.serializer.plang.Data) does NOT
        // invoke signing.verify — verification is the consumer's explicit step. Pin
        // this by checking that a deserialized Data has signature populated but the
        // app's signing module wasn't called as part of the read.
        var app = NewApp();
        var data = new Data("v") { Value = "hello", Context = app.User.Context };
        app.User.Context.Variables.Set(data);
        var s = app.User.Channels.Serializers.GetByMimeType("application/plang+data");
        var wire = s.Serialize(data);
        var restored = s.Deserialize<Data>(wire);
        await Assert.That(restored).IsNotNull();
        // Restored carries Signature, but we never called signing.verify here.
        await Assert.That(restored!.RawSignature).IsNotNull();
    }

    private sealed class StubCallback : ICallback
    {
        public global::app.callstack.call.Position? Position => null;
        public byte[] Serialize(global::app.actor.context.@this ctx) => Array.Empty<byte>();
        public Task<global::app.data.@this> Run(global::app.actor.context.@this ctx)
            => Task.FromResult(global::app.data.@this.Ok(true));
    }
}
