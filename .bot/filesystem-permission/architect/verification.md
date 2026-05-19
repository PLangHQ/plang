# Verification — Plan vs Existing Code

Pass done 2026-05-19 after the design walkthrough. Cross-references each plan assumption against actual `PLang/App/` code. **All findings resolved in the 2026-05-19 third-pass rewrite** — see status notes inline. This file is now historical; the resolved findings are baked into the stage files.

## Confirmed (plan matches reality)

### Actor model — three kinds as strings
**Plan claim:** Actor is `"system" | "user" | "service"`; no per-instance IDs.
**Code:** `PLang/App/Actor/this.cs:17-19` — `public string Name { get; }` documented as `"System" / "Service" / "User"`. Three kinds, strings on Actor.@this. Confirmed.

### App identity — `App.@this.Id` exists
**Plan claim:** `FilePermission.AppId` maps to the app's identity.
**Code:** `PLang/App/this.cs:34` — `public string Id { get; set; }`, 12-char GUID loaded from app.pr or generated (line 291). The plan's `AppId` field is `App.@this.Id`. Confirmed.

### 2-column storage shape is already enforced
**Plan claim:** Every actor-scoped table is two columns (`id`, `data`).
**Code:** `PLang/App/Settings/Sqlite.cs:305-311` — `EnsureTable` auto-creates each table on first use with exactly `(key TEXT PRIMARY KEY, data TEXT)`. **Note: the column name is `key`, not `id`** — the plan said `id`; update plan text to match.

### Signed Data round-trip via Plang serializer
**Plan claim:** Stored Data carries Signature; serializer round-trips it.
**Code:** `PLang/App/Channels/Serializers/Serializer/Plang/Data.cs:9` — serializer explicitly handles `Type + Value + Signature` envelope. Settings uses this path. Confirmed.

### AskError already exists
**Code:** `PLang/App/Errors/AskError.cs` — documented as: *"Runtime handling (prompt-store-retry) is out of scope — comes in a separate branch."* This branch is literally what that comment was anticipating. AskError today carries `Table + DataKey` (settings-shaped); we'd extend the pattern for FilePermissionAsk or add a sibling class.

## Wrong (plan diverges from existing infrastructure)

### `error.handle`'s "built-in path" doesn't exist  ✅ RESOLVED (stage 2 rewritten around callback-suspension)

**Plan claim:** "`error.handle` is the umbrella — its built-in path recognises Ask-marked errors and runs the consent/input flow before user-configured handlers."
**Code:** `PLang/App/modules/error/handle.cs` — error.handle is a **user-applied Modifier** (`IModifier`, `Wrap(next, context)` pattern, `[Modifier(Order = 3)]`). It runs only when a developer attached `on error, ...` to a step. There is NO runtime-resident "built-in path" that intercepts errors before user modifiers see them.

**Implication for the plan:** stage 2's framing is wrong. Ask routing has to live somewhere else — either:
- A new always-on modifier with lower `[Modifier(Order = ...)]` (runs before error.handle in the modifier chain).
- Engine-level interception before modifiers run at all.
- A different pattern entirely — see next finding.

### Ask routing pattern is callback-suspension, not error-marker  ✅ RESOLVED (stage 2 rewritten — `PermissionAskCallback : ICallback`)

**Plan claim:** Action returns `Data.Fail` with an `Ask` marker error; something downstream routes it.
**Code:** `PLang/App/modules/output/ask.cs` + `PLang/App/Callback/AskCallback.cs` — `output.ask` returns `Data.@this<AskCallback>` (typed Data, success path, NOT a Fail). The Data carries an `ICallback` instance. The channel detects the callback in the result and suspends the goal. On user answer, `AskCallback.Run(ctx)` re-dispatches the original action (`ctx.App.Run(Position.Action, ctx)`) with the answer pre-bound under `!ask.answer`.

The action handler short-circuits on the second invocation: sees `!ask.answer` is set, returns it as the result.

**Implication for the plan:** the right pattern for permission asks is NOT "Data.Fail with Ask marker handled by error.handle." It's:

- `file.read` (or rather PLangFileSystem) returns `Data.@this<PermissionAskCallback>` (typed Data, success path) when permission missing.
- Channel detects the callback, serializes it, suspends.
- User responds (consent UI driven by the channel — same as how the existing ask UI works).
- Resumed call: `PermissionAskCallback.Run` signs the grant, stores it via `actor.Permission.Add(signed)`, re-dispatches `file.read` through `App.Run(Position.Action, ctx)`.
- Second `file.read` invocation: permission check passes (grant is now in the actor's Permission), returns content.

This is structurally cleaner and reuses existing PLang infrastructure (Callback, channel suspension, Position resume). The plan needs a substantial rewrite of stage 2 to reflect this.

### Actor has no Snapshot extension  ✅ RESOLVED as known limitation (stage 3 calls out: "y" grants don't survive snapshot; acceptable for v1)

**Plan claim:** "Session grants are in-memory on `Actor.@this.Permission`. If the App is paused via snapshot, the list is captured as part of App's state."
**Code:** `find PLang/App -name "this.Snapshot*"` shows snapshot partials for App, Tester, Builder, Variables, CallStack, Code, Statics, Errors, Call — but **NOT for Actor**. Actor is not snapshot-aware today.

**Implication:** in-memory grants on `Actor.@this.Permission` would NOT ride the existing snapshot path automatically. Options:
- Add `Actor.this.Snapshot.cs` implementing `ISnapshot` — captures the permission list (and any future per-actor state).
- Park in-memory grants and only use the persisted path (sqlite) — loses the "y" UX nicety.
- Put session grants in the actor's `Variables` (which IS snapshot-aware) — but that conflates two concerns.

The plan should either add a snapshot extension for Actor as part of stage 3, or explicitly say "session grants don't survive snapshot" as a known limitation.

## Smaller specifics to nail down in plan

### `path.Authorize(verb)` — parameter type  ✅ RESOLVED (option 1: overload taking the verb record — `path.Authorize(new Read())`)

The plan punts this to "coder picks." But three reasonable shapes:
- Method overload taking the record: `path.CheckPermission(Read r)` / `(Write w)` / `(Delete d)`. Most natural in C#; passing `new Read()` says "default full Read," `new Read(Recursive: false)` narrows further.
- Enum: `VerbKind { Read, Write, Delete }`. Simpler caller, less expressive (can't specify sub-options at call site).
- Generic: `path.CheckPermission<TVerb>()`. Type-level; awkward for the narrow-the-sub-option case.

The overload pattern (option 1) is the most PLang-natural. Records-as-parameters; whoever calls knows what they need.

### `IStore` API today doesn't have `AddOrUpdate`  ✅ RESOLVED (stage 3 uses `IStore.Set(table, key, data)`)

**Code:** `PLang/App/Settings/IStore.cs` exposes `Set(table, key, data)` which is upsert behaviour (INSERT OR REPLACE). The plan said `Settings.AddOrUpdate(data)` — actual API is `Set(table, key, data)`. Plan text should use `Set` to match.

### `IStore.GetAll(table)` returns `Data.@this` containing `List<Data>`
**Code:** `PLang/App/Settings/Sqlite.cs:135-163`. Plan was correct in spirit; just confirming.

### Multi-path Ask bundling shape isn't specified
The plan says "bundled Data.Fail whose Ask carries the full list." Concrete shape options:
- `FilePermissionAsk(List<FilePermission>)` — single Ask carrying a list.
- A new `BatchAsk : Ask` wrapping multiple single Asks — composable.
- Multiple Data.Fail.Errors? Awkward.

The first is simplest if we keep the Ask-as-error pattern. **But:** since the better pattern is callback-suspension (see above), the bundled shape becomes "the callback carries a list of FilePermission descriptions that the channel prompts about as one consent." Move resolution to the next plan revision.

## Ingi's calls on these findings (2026-05-19 conversation)

- **Callback pattern confirmed.** Stage 2 to be rewritten around `PermissionAskCallback : ICallback`. The same AskCallback machinery handles permission asks — same suspension model, same channel detection, same `Run()` resume path. Permission's callback differs only in what it carries (FilePermission asks) and what it does on resume (sign + store + re-dispatch the original action).
- **Inside `error.handle` framing dropped.** Not the right home. The Ask pattern lives at the action-return layer (typed Data carrying an ICallback), not in error.handle.
- **Actor snapshot deferred.** Don't add `Actor.this.Snapshot.cs` in this branch. Note as a known limitation: in-memory ("y") grants don't survive snapshot/restore. If the App is paused mid-flow, "y" grants are lost — the user re-prompts on resume. Acceptable for v1.
- **Per-kind keying in `IStore.Set`.** Each permission kind decides its own keying strategy. For `FilePermission`, the natural key is the **path** itself. So `actor.Permission.Set(...)` for a file grant uses the absolute path string as the key. Two implications:
  - Granting the same path twice overwrites the previous grant — idempotent, by design.
  - Glob grants (`/apps/*/file.txt`) and exact grants (`/apps/Email/file.txt`) are different keys, coexist naturally.
  - Per-kind keying logic lives close to the kind — likely a method on the record (`FilePermission.Key { get; }` returning the path, or similar). HttpPermission later picks URL-pattern; PaymentPermission picks recipient; etc.

## Recommendation for fresh-eyes review

The plan has the **design intent** right — pure-data records, Path owns CheckPermission, actor scoping via JSON filter, 2-column tables, etc. The **integration layer** with PLang's existing infrastructure is wrong in two important places:

1. **Stage 2 needs rewriting** around callback-suspension, not error-marker-with-error.handle-built-in-path. The pattern already exists (`output.ask` + `AskCallback`); permission asks should mirror it.
2. **Snapshot for actor in-memory state needs explicit design** — either add it to stage 3 (with an `Actor.this.Snapshot.cs`), or remove the snapshot claim from the plan.

Smaller corrections:
- `key` not `id` for the column name.
- `IStore.Set` not `AddOrUpdate`.
- `path.CheckPermission(Read r)` overload pattern instead of a generic verb-kind parameter.

The OBP correctness work — pure records, Path owning its check, no helper soup, "Data is a Data object" — that all still holds. It's the wiring into PLang's runtime that was hand-waved.
