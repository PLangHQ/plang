# Births step B — two clusters don't clear the way the plan expects

The working tree holds step B, uncommitted: App as birth fact for Data, list, dict, source, wire, the
path family (file/url/directory through their path), computed and clr. Context is get-only through
`_app.Context`; every setter and stamp is gone; `Context.App` → `App` inside values; Authorize is in-root
first; `action[name]` hands out the copy; the generator doesn't stamp. It builds clean, tests included.

Six suites, by name, against step A (26823c1d1), after one test-side fix (below): **155 new failures.**

## Test-side fix already in the tree

`TestApp.SharedContext` (used by the static factories `Make.*` and `PrParam`) now means "the current test's
own app" when one was built in this flow; before, it was a process-wide shared app. A value lives in the
App it was born in, so fixture rows born in the shared app read the shared app's run, which is never set
in the test's flow (183 → 99 "No run in progress" on that fix alone). Same principle, test side.

## Cluster 1 — values need a run just to BIRTH a result (≈ 99 "No run in progress")

Top frames that ask `Context` outside any run:

| Where | Count | What for |
|---|---:|---|
| `path.Authorize` / `AuthGate` | 25 | out-of-root checks, which legitimately need an actor |
| `path.Mkdir` ← `Sqlite.CreateAsync` ← `App.CreateSettingsStoreAsync` | 22 | result Data (`Context.Ok/Error`) for an in-root op |
| `path.WriteText/ExistsAsync/Stat/List/Delete/Append/WriteBytes`, `http.ReadText` | ~30 | mostly result Data births |
| `dict.Slot/Get/Clr`, `list.Row/Items`, `data.Get` | ~40 | birthing the element Data a read hands back |
| `permission.Add`, `identity.LoadAll`, `condition.EvaluateOperator`, `Json.SerializeAsync` | ~25 | mixed |

Most of these don't need an actor: they use `Context` as the **factory for the Data they hand back**
(`Context.Ok(x)`, `new Data(key, …, context: _context)`), and that Data now keeps only the App from it.
That contradicts plan §3: "reading anything in-root then needs no actor at all". An in-root
`WriteText`, a list element read, or the settings store's creation should not need a run, yet each
throws outside one, because its result birth reads the running context.

**Proposal:** values birth what they hand back **through their App**, not the running context: an App-level
birth door (context-free) for result/element Data, so only the operations that truly need an actor (an
out-of-root Authorize, a %var% resolve, a channel ask) require a run. The shape is yours to rule. Two
candidates:
- (a) a context-free Data birth from an App. `Data` already stores only `_app`; a constructor or factory
  taking the App directly (`new Data(name, value, app)` / `app.Ok(value)`). Item lifts inside it need
  the same (items store only their App now too, so `type.Create(raw, …)` would take the App).
- (b) keep the `context` parameters but hand values a context of their App that needs no run. It works
  (a birth keeps only `.App`), but reads as "System" or similar, which lies about what it is. I'd avoid it.

## Cluster 2 — the run slot doesn't reach C# callers that aren't in a run (≈ 56 others, plus part of cluster 1)

- **Apps built inside async helpers.** `TestApp.Create` sets the slot, but when called inside an async
  helper (`WriteAndRead`, `NewApp()` inside `async Task` methods), AsyncLocal scoping ends the set when
  the helper returns. The test body then reads values with no run. That's the design working, but it hits
  every test that builds its app in an async helper. The symptom is silent: values come back `"null"`
  (Cut1/Cut2/Cut4 round-trips, HTTP responses, `Goal 'null' not found`, verify false).
- **Bare apps.** `new app.@this(root)` fixtures (FileSystemPermissionFlowTests alone is 60 frames) never
  set a run.
- **Direct handler calls.** `new X(ctx).Run()` skips `action.Run`, the door.

These are C# callers, not plang runs. The plan's TUnit hook covers "app built in Setup, used in body" and
nothing else.

**Options:**
- (a) Cluster 1's App-born results take most of these out: an in-root verb or an element read then needs
  no run. What's left is the genuinely actor-needing tests, which must run as someone.
- (b) For those: `TestApp.Create`/`Plain` return the app, and a test that needs an actor says so
  explicitly: `await using var run = app.RunAs(app.User)`, a scope that sets the slot for the flow. Visible,
  test-side, no production fallback.
- (c) Not recommended: a production fallback (e.g. test-mode apps default to User outside a run). It's a
  hidden context by another name.

## Also found

- The silent `"null"` in cluster 2 means a no-run read doesn't always throw. `source`'s `%var%` path and
  some readers catch or propagate as an absent value rather than loud. That needs its own look once the
  shape is ruled, because it's the "loud error, no fallback" rule leaking.
- Nothing else in production outside tests showed up as an outside-run door yet. Boot (`Start`), builder
  (`goal.Run`) and `goal.call` all go through the doors. The settings store is lazily created inside
  whichever run first touches it, which is fine once its path ops birth through the App (cluster 1).

## State

- Step A committed and pushed (26823c1d1).
- Step B uncommitted, building, 155 new reds, waiting for the shape of cluster 1 (App-born results) and
  cluster 2 (how C# callers outside a run get one).
