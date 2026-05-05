# Smallest meaningful first cuts

Two test shapes, layered. First cut establishes the resume mechanism. Second cut adds the durability layer. Everything beyond it (encryption, wire transport) is mechanical. **Concrete tests are test-designer's job — this file describes what each first cut needs to prove.**

## First cut — in-process resume

One test. No wire, no storage, no UI.

**Setup:** A goal with three actions: `set %x%=1`, then a throw, then `set %x%=2`. Run the App; it throws.

**Capture:** Read `%!error.callback%` from the failed App's context to get a `Data<Callback>`.

**Resume:** Construct a fresh `App` and pass the callback to `App.Run(callback)`.

**What it must prove:**

- The resumed App lands at the failed action and re-executes it. With the throw condition removed, the action that previously threw now succeeds. The next action (`set %x%=2`) runs. After resume, `%x% == 2`.
- The captured callback's `VariablesByActor` reflects the **throw-time view**, not the post-handler state. So `callback.VariablesByActor["x"] == 1` even if the error handler mutated `%x%` between throw and capture.

If this passes, the keystone is in: bind-jump-run, throw-time variable capture, position semantics, snapshot/restore of `ISnapshotted` types.

## Second cut — durability round-trip

One test. Two-process simulation (one `App` instance per "process").

**Issue (process A):** Same setup as the in-process test — goal throws.

**Persist:** Serialize the `Data<Callback>` through `App.Channels.Serializers`. The serializer auto-signs (no explicit `- sign` call).

**Fresh process (B):** Construct a new `App`. Deserialize the bytes back into `Data<Callback>` (signature comes back populated, unverified).

**Verify:** Invoke the `signing.verify` action on the deserialized envelope. Look up the goal by `GoalPrPath`. Compare `goal.Hash` against `callback.GoalHash`.

**Resume:** `App.Run(callback)` on a third `App`.

**What it must prove:**

- Signature populates automatically on serialize, verifies cleanly on read.
- `Goals.LoadByPrPath(GoalPrPath)` finds the goal; hash matches.
- Resume runs the same way as in-process: `%x% == 2` after resume; `callback.VariablesByActor["x"] == 1`.

If this passes, the durability stack is green: transparent signing, storage round-trip, signature verification, goal-hash gating, snapshot restore across the serialization boundary.

## Note on framework

PLang uses TUnit. Test-designer translates these shapes into TUnit assertions (`await Assert.That(...)` etc.); the architect doesn't pick attributes or assertion style.
