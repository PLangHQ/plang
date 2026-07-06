# CallStack knobs-as-settings (agreed with Ingi 2026-07-06) — Stage 4 piece

Ingi's call: the CallStack knobs should be **settings** — the same model as the http action's
properties (each param IS a setting) and as `--build`/`--debug` (configured at startup). NOT a
`CallStack.Setting` record (the plan's Q1 reshape is superseded by this).

## The model
`CallStack.Flags` (record struct) + `Flags.Parse` + `Flags.Shorthand` → **gone**. The knobs become
**resolving properties on CallStack** that read the setting store, each with its own `[Default]`:
```csharp
public bool Timing    => App.Setting.Peek("callstack.timing")   is @bool b && b.Value;   // default false
public bool Diff      => App.Setting.Peek("callstack.diff")     is @bool b && b.Value;
public bool DeepDiff  => App.Setting.Peek("callstack.deepdiff") is @bool b && b.Value;
public bool Tags      => App.Setting.Peek("callstack.tags")     is @bool b && b.Value;
public bool History   => App.Setting.Peek("callstack.history")  is @bool b && b.Value;
public int  MaxFrames => App.Setting.Peek("callstack.maxframes") is number n ? n.ToInt32() : 1000;
```
Read sites unchanged in spirit: `stack.Flags.Timing` → `stack.Timing`, `stack.Flags.History && count > stack.Flags.MaxFrames` → `stack.History && count > stack.MaxFrames`.

`--callstack={"timing":true}` **writes the `callstack.timing` setting** (startup) — no `Flags.Parse`
translating a dict into a struct. No `--callstack=true` Shorthand (Q1: Shorthand dies).

## Decisions locked
- **Sync read is fine** — set at startup, stable. Add `data.@this? Peek(params keys)` to
  `app.setting.@this` (sync in-memory walk of `_values`, no await) for these hot/sync reads.
- **Error-recovery flip** (`error/list/this.cs:80`, temp Diff on) → a setting override + restore.
  Prefer **context-scoped** (write on the recovery-scope context, auto-restores when the scope ends —
  no save/restore) **if** the recovery calls are children of that context; else app-level override + restore.
- Kills the torn-struct concurrency note (per-knob setting read is atomic).

## Implementation touchpoints (Stage 4)
1. **CallStack needs App access** — it's `new()` today (`app/this.cs:280`), holds no App/context. Wire
   it (`new(this)` / an `App` property) so the knobs can reach `App.Setting`. Touches construction.
2. `app.setting.@this.Peek(params keys)` — sync in-memory read.
3. CallStack knob properties (above); delete the `Flags` field.
4. Read-site updates: `callstack/call/this.cs` (Push-time Timing/Diff), `callstack/call/child/list/this.cs:34`
   (History/MaxFrames), `callstack/this.Snapshot.cs` (Diff).
5. The flip: `error/list/this.cs:79-81,91-111` (`Flags with { Diff = true }` + Restorer) → setting override.
6. Delete `callstack/Flags.cs` (struct + Parse + Shorthand).
7. CLI: an Executor `!callstack` branch writing `callstack.*` settings; remove Debug.Apply's callstack
   cross-node write (`debug/this.cs:130-134` — `CallStack.Flags = Flags.Parse(...)`).

This is a coherent but multi-file Stage-4 change (touches CallStack construction + the error-recovery
flip). Ready to implement as a focused block.
