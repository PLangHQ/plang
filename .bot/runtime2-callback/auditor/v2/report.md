# auditor v2 — runtime2-callback

**Scope:** independent re-audit on the state at `b7f7c526` (security v1 PASS). No coder fixes have landed since auditor v1 (`3fbc7cfa`); the v2 task is to confirm or push back on security v1's findings (1 Medium + 3 Lows) before any coder pass touches them.

**Verdict:** PASS — all four security findings confirmed by independent reading. One small bug in security's *suggested fix snippet* for F2 worth flagging so coder doesn't paste the broken version. One observation security didn't lift (a misleading test name that cements the F1 bypass) called out below.

---

## Confirmation walk

### S-F1 — Medium: `callback.run` skips verify when `RawSignature == null` — CONFIRMED

`PLang/App/modules/callback/run.cs:27-35` reads exactly as security described:

```csharp
if (Callback.RawSignature != null) {
    var verifyResult = await Context.App.RunAction<verify>(...);
    if (!verifyResult.Success) return Data.FromError(...CallbackSignatureMismatch...);
}
return await cb.Run(Context);
```

`PlangDataSerializer.FromEnvelope` (`Channels/Serializers/Serializer/PlangDataSerializer.cs:115-121`) populates `d.Signature = env.Signature` straight from the wire — null is a legal envelope shape. The handler's gate, in its current form, is "absence-of-signature == trust", which is the correct posture for in-process construction and the wrong posture for wire-arriving Data.

**Practical bound today:** verified. No HTTP/channel handler currently calls `PlangDataSerializer.Deserialize<Data>` and feeds the result into `callback.run`. `AskCallback.Deserialize` and `ErrorCallback.Deserialize` are wired only to tests. The bypass is *latent* — it materialises the moment any channel constructs a `Data<ICallback>` from an inbound envelope and hands it to `callback.run`.

**Recommendation alignment:** option (2) in security v1 — auto-`EnsureSigned` on dispatch and require `RawSignature != null` post-Ensure — is the right shape. It removes the in-process / wire branch from the handler entirely. Auditor concurs.

**Severity:** Medium is correct. Reads as Medium-trending-High the moment Stage 5 lands; pre-merge fix preferred.

---

### S-F1-adjacent — Misleading test name (auditor adds)

`PLang.Tests/App/CallbackTests/CallbackRunActionTests.cs:22-30`:

```csharp
[Test]
public async Task CallbackRun_VerifiesSignature_BeforeDispatch()
{
    // No signature on the Data → handler skips verify. Call dispatches and returns.
    ...
    await Assert.That(result.Success).IsTrue();
}
```

The test *name* claims it verifies the signature gate; the test *body* asserts the unsigned-bypass path returns Success. The name should read `CallbackRun_SkipsVerify_WhenNoSignature` (or similar) so the test pins the *current* behaviour without claiming the *desired* behaviour.

When S-F1 is fixed under option (2), this test must flip its assertion (Success → false, or be replaced by an in-process-with-EnsureSigned positive test). Tester v1 #3 noted the name/assertion mismatch; security v1 noted the test "pins the bypass". Auditor concurs and flags it as the load-bearing renaming/inversion that comes with the fix.

Not a separate finding — bundled with S-F1.

---

### S-F2 — Low: `Variables.Restore` and `ErrorCallback` wire path don't filter `!`-prefix — CONFIRMED

`PLang/App/Variables/this.Snapshot.cs:35-46`:

```csharp
public static void Restore(Snapshot.@this s, Actor.Context.@this ctx)
{
    var captured = s.Read<List<Data.@this>>("variables");
    if (captured == null) return;
    var target = ctx.App.Variables;
    foreach (var data in captured)
        target.Set(data.Name, data.Clone());
}
```

No filter. Security's read is correct: `Capture` filters `!`-prefix outbound, `Restore` does not filter inbound; combined with S-F1, an unsigned `ErrorCallback` wire injects `!`-prefix names verbatim (`!ask.answer`, `!error.*`).

**🐛 Bug in security v1's suggested fix snippet** — worth flagging so coder doesn't paste it as-is:

```csharp
// security v1 summary.md, lines 159-163:
foreach (var data in captured)
{
    if (!string.IsNullOrEmpty(data.Name) && !data.Name.StartsWith("!")) continue;
    target.Set(data.Name, data.Clone());
}
```

This `continue` condition is **inverted**. As written it `continue`s when the name is *safe* (non-empty AND non-`!`-prefix), and only `Set`s the *unsafe* names — exactly opposite to the intent. The correct shapes are:

```csharp
// option A: invert the condition
foreach (var data in captured)
{
    if (string.IsNullOrEmpty(data.Name) || data.Name.StartsWith("!")) continue;
    target.Set(data.Name, data.Clone());
}

// option B: include-style, mirrors the auditor v1 AskCallback fix in 3fbc7cfa:
foreach (var data in captured)
{
    if (!string.IsNullOrEmpty(data.Name) && !data.Name.StartsWith("!"))
        target.Set(data.Name, data.Clone());
}
```

Prose intent is unambiguous; the snippet is just transcription drift. Coder should use option A or B verbatim.

**Severity:** Low is correct. (Becomes Medium under S-F1 unfixed, since it amplifies the bypass.)

---

### S-F3 — Low: no size/depth caps on callback-wire JSON deserialization — CONFIRMED

`AskCallback.cs` `_options` and `ErrorCallback.cs:149-154` `_options` both lack `MaxBytes` / `MaxVariables` and rely on the default `JsonSerializerOptions.MaxDepth = 64`. `Data.@this.GZipDecompress` enforces `MaxDecompressedSize = 100MB` — verified, but this path doesn't go through gzip. Bound today is "no channel feeds wires"; bound tomorrow has to come from the channel's size cap plus a defense-in-depth cap on the callback layer.

**Severity:** Low is correct. Defense-in-depth.

---

### S-F4 — Low: callback-wire serializers don't honour `[Sensitive]` — CONFIRMED

`AskCallback.cs:91-96`, `ErrorCallback.cs:149-154`, `PlangDataSerializer.cs:22-29` — none of the three option blocks have the `SensitivePropertyFilter.Strip` modifier that `Data.@this`'s envelope JSON options apply. `Variables.Capture` clones the full Data shape (not just k→v), so any `[Sensitive]` property on a captured Variable's Value object crosses the wire un-redacted.

Practical scope today is narrow — `Identity.PrivateKey` is the only shipping `[Sensitive]` property and Identity flows through Settings (which `Variables.Capture` excludes via SettingsVariable skip). The *architectural* gap matters because `[Sensitive]` will accumulate.

**Severity:** Low is correct.

---

## Adjacent risks (re-walked)

- **`Providers.Restore` + `Assembly.LoadFrom`:** confirmed inert via the wire path. `App.Restore` (`this.Snapshot.cs:43-49`) gates each subsystem on `s.HasSection(...)`; ErrorCallback's `DeserializeSnapshot` writes only `CallStack` and `Variables` sections, so `Providers.@this.Restore` is never invoked. Security v1's note ("immediate Critical if ErrorCallback wire is ever extended to round-trip Providers") is the correct architectural fence to hold.
- **CLR exceptions out of Restore (auditor v1 N3):** still open. Combines with S-F1: a wire-arriving ErrorCallback with a bad position throws unhandled out of the dispatch path. The fix shape (wrap in `Data.FromError(IError)` at the public entry points) belongs in the same coder pass that addresses S-F1.
- **Inert wire fields (`ActorName`, `ActionModule`, `ActionName`, `Id`):** confirmed carried-but-unread. Architectural decision (drop vs. keep) belongs to the architect, not the security/auditor surface.

---

## OBP (no new types since v1)

No new types since auditor v1 — the v1 OBP table stands. Spot-checked the new lines around `Restore`/`callback.run`/serializer options; nothing leaks state outside the owning `@this`.

---

## Verdict matrix

| Finding | Severity | Confirmed? | Blocking |
|---|---|---|---|
| S-F1 — verify-skip when `RawSignature == null` | Medium | ✅ | Before any channel wires inbound callbacks (Stage 5+). Recommend pre-merge under option (2). |
| S-F2 — `!`-prefix filter missing on `Variables.Restore` | Low | ✅ | Bundle with S-F1. **Use option A or B from this report — security v1's snippet is logically inverted.** |
| S-F3 — no size caps on callback wire | Low | ✅ | Defense-in-depth; defer to channel-wiring stage if channel-layer bound is firm. |
| S-F4 — `[Sensitive]` not applied to callback-wire serializers | Low | ✅ | One-line per call site; bundle with S-F1/S-F2. |
| auditor v1 N1/N2/N3 | Note | (unchanged) | Document or fix when stage-5 lands. |

**`pass`** — branch is mergeable on the auditor's gate. Security v1's recommendation stands; auditor v2 reinforces option (2) for S-F1 and adds the F2 snippet correction.

---

## What was done

- `git checkout runtime2-callback`; read auditor v1 / tester v1 / security v1 reports.
- Re-walked the wire-touch surface from sources (no rebuild — no code changed since `b7f7c526`):
  `App/modules/callback/run.cs`, `App/Callback/{AskCallback,ErrorCallback}.cs`,
  `App/Variables/this.Snapshot.cs`, `App/this.Snapshot.cs`,
  `App/Channels/Serializers/Serializer/PlangDataSerializer.cs`,
  `PLang.Tests/App/CallbackTests/CallbackRunActionTests.cs`.
- Confirmed each S-F1..S-F4 from primary sources; transcribed the F2 snippet defect.
- No code edits — auditor reports, doesn't change code (per `98596b63` proposal).
