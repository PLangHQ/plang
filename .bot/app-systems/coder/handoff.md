# handoff — 375 (1) mid-flight

Committed: data reader births a received error as a failed Data; error reader IsEager; FailedWriteTests pin (green). Gate + ptest NOT yet run on this commit.

Open: http pin `AResponseCarryingAFailure_FailsTheRequest_WithThatError` fails with NoSignature.
Remote app must sign for real (plain app, not .Testing()); receiver's verify now says NoSignature — next: trace how verify finds the signature on a Data read back failed (the failed Data may lose the signature layer).

```diff
diff --git a/PLang.Tests/Modules/App/Modules/http/RequestActionTests.cs b/PLang.Tests/Modules/App/Modules/http/RequestActionTests.cs
index 3dd7dd2d4..be98c2954 100644
--- a/PLang.Tests/Modules/App/Modules/http/RequestActionTests.cs
+++ b/PLang.Tests/Modules/App/Modules/http/RequestActionTests.cs
@@ -175,6 +175,33 @@ public class RequestActionTests
         await Assert.That(sent).DoesNotContain("note.txt");
     }
 
+    // A remote failure: another app answers with its failed result, written in plang. The request that reads it
+    // fails with that error — the step fails and its on error runs.
+    [Test]
+    public async Task AResponseCarryingAFailure_FailsTheRequest_WithThatError()
+    {
+        await using var remote = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
+            "plang_remote_" + Guid.NewGuid().ToString("N")[..8]));
+        var remoteCtx = remote.actor.list.User.Context;
+        using var body = new System.IO.MemoryStream();
+        await remoteCtx.Format("application/plang").Encode(body,
+            remoteCtx.Error(new global::app.error.Error("the disk is full", "DiskFull", 507)), remoteCtx);
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
+
     [Test]
     public async Task Post_FormUrlEncoded_SendsCorrectContentType()
     {
```
