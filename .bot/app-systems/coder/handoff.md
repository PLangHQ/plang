# handoff — 375 (1) done; http plang-response path open

375 (1) is committed: a Data read holding an error is born failed with that error. Pinned between two real-signing
apps (`FailedWriteTests.AFailedResult_SignedBetweenActors_ArrivesAsTheSameFailure`). Underneath: `crypto.hash`
took its Data unwhole, so under a real signer a failed Data could not be encoded to plang at all — `Hash.Data`
is `[Whole]` now (like `sign.Data`) and a failed Data hashes its error.

## Open — waiting on the architect

`http.request` reading an `application/plang` response (`http/code/Default.cs` ParsePlangResponseAsync) fails
for any really-signed body (only `"{}"` stubs test it today):

1. It decodes a transport response in the **Store** view → verify rehashes in Store (adds `name`), the sender
   signed in Out → `DataHashMismatch`.
2. `Decode` already verifies and peels the signature layer; the function verifies again on the peeled Data →
   `NoSignature`.
3. `!ServiceIdentity` reads the signature layer off the already-peeled Data → always null.

The pin that exposes it (not committed — it fails until the path is reshaped):

```diff
+    [Test]
+    public async Task AResponseCarryingAFailure_FailsTheRequest_WithThatError()
+    {
+        await using var remote = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
+            "plang_remote_" + Guid.NewGuid().ToString("N")[..8]));
+        var remoteCtx = remote.actor.list.User.Context;
+        using var body = new System.IO.MemoryStream();
+        var encoded = await remoteCtx.Format("application/plang").Encode(body,
+            remoteCtx.Error(new global::app.error.Error("the disk is full", "DiskFull", 507)), remoteCtx);
+        await encoded.IsSuccess();
+        var bytes = body.ToArray();
+        _handler.Handler = _ =>
+        {
+            var content = new ByteArrayContent(bytes);
+            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/plang");
+            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
+        };
+
+        var action = new request(Ctx) { Url = (global::app.type.item.text.@this)"https://api.example.com/disk" };
+        var result = await new global::app.goal.step.action.@this(action, Ctx).Start(Ctx);
+
+        await result.IsFailure();
+        await Assert.That(result.Error!.Key).IsEqualTo("DiskFull");
+        await Assert.That(result.Error.Message).IsEqualTo("the disk is full");
+    }
```
