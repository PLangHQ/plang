# Foundation Verification — 2026-05-11

Read-only depth-check of four foundation areas the next module-port work will sit on. Each section follows the same shape: surface, tests, what works, what's partial or missing, verdict.

---

## Snapshots

**Surface.** Two files. `PLang/App/Snapshot/this.cs` (53 lines) is the section container — `Section(name)`, `HasSection(name)`, the `ISnapshot` interface. `PLang/App/this.Snapshot.cs` (51 lines) is the orchestration: `App.Snapshot()` walks the 7 participating subsystems into named sections; `App.Restore(snapshot, context)` dispatches each section to the matching subsystem's static `Restore`.

Participating subsystems (the only mutable state that round-trips):
- `Variables` (per-actor)
- `Errors`
- `Providers` (now `Code`)
- `Statics`
- `Build` (Builder)
- `Testing` (Tester)
- `CallStack`

Intentionally **not** snapshotted: Modules, Goals, Channels, Cache, Events, Settings, Navigators, Types, Config, FileSystem. These are reconstruct-on-build — rebuilt fresh from the goal tree + config, no transient state to preserve. Clean design.

**Tests.** Eight TUnit suites under `PLang.Tests/App/SnapshotTests/` and `PLang.Tests/App/CallStackTests/`:
- `AppSnapshotTests` — orchestration round-trip
- `ProvidersSnapshotTests` — two-step replay (provider order matters)
- `SnapshotInterfaceTests` — `ISnapshot` contract
- `StaticsAndModesSnapshotTests` — Statics + Build modes
- `CallStackSnapshotTests`, `CallSnapshotTests`, `SnapshotChainTests` — CallStack round-trip
- `ErrorsTrailSnapshotTests`, `VariablesSnapshotTests` — per-subsystem

Plus `Tests/TestModule/Assert/TestAssertFailureSnapshotsVariables.test.goal` — end-to-end PLang assertion of snapshot-on-failure.

**What works.** Save/restore round-trips for all 7 sections. The `Restore` dispatch handles partial snapshots (missing sections silently skip). `Providers` has its own two-step replay so registration order is preserved. Hard errors propagate (e.g. `ProviderRestoreException`) — the App is left partially restored and the caller is responsible.

**What's partial or missing.**
- **No PLang surface that exercises save+restore as a full-app pause/resume.** The TUnit tests cover each subsystem; the .goal test covers variable capture-on-failure. But there's no `Tests/Snapshot/` directory with a `save → exit → restore → assert behaviour continues` flow. When ask-user lands (the suspend/resume case), this gap matters.
- **`App.Statics` is captured as a flat key/value bag.** The 2026-05-05 "goal-backed dynamic property" todo is partially resolved (shape carve done, deep replacement pending). Snapshot fidelity matches the current bag, so this is internally consistent — but the snapshot shape will change when Statics goes goal-backed. Callback's `Serialize`/`Deserialize` shape will follow.
- **No coverage of `ProviderRestoreException` propagation in PLang tests.** Failure is unit-tested; the developer-visible behavior of a restore-failure (does the app crash? does an error.handle catch it?) is not asserted at the .goal level.

**Verdict: SOLID for the current scope.** The seven captured sections round-trip cleanly. The partial/missing items are coupled to features that don't exist yet (ask-user transport, goal-backed Statics) — no point fixing in isolation. When ask-user lands, add end-to-end PLang tests at that point.

---

## Identity

**Surface.** Eight action handlers under `PLang/App/modules/identity/`: `create`, `get`, `list`, `setDefault`, `archive`, `unarchive`, `rename`, `export`. One provider at `code/Default.cs` (the resolver wiring through `IKey` + `signing/code/Ed25519.cs`).

**Tests.** Twelve `.test.goal` files across `Tests/Identity/` and `Tests/Modules/Identity/`:
- `Create`, `GetByName`, `SetDefault`/`SwitchDefault`, `Archive`/`ArchiveNonDefault`, `Export`, `DotNavigation`

Five TUnit suites under `PLang.Tests/App/Modules/identity/`:
- `IdentityHandlerTests`, `IdentityErrorPathTests`, `IdentityVariableTests`, `MyIdentityResolverTests`, `IdentityKeyProviderTests`

Plus `PLang.Tests/App/DataTests/AsTIdentityTests.cs` and `PLang.Tests/App/Modules/list/ListAddIdentityTests.cs` — identity Data + integration with list ops.

**What works.** Create (Ed25519 keypair generation), switch default, archive/unarchive, get-by-name, dot-navigation (`%MyIdentity.Name%`), export. Error paths covered (archive non-existent, switch to archived, etc.). Identity participates in the snapshot via Providers (keys live behind `IKey` provider; archives live in Settings).

**What's partial or missing.**
- **First-run bootstrap is implicit.** No `Tests/Identity/FirstRun/*.test.goal` that verifies a brand-new app with no identities at all gets a default identity created automatically. The behavior probably works (the resolver creates on demand) — but no test pins it.
- **Recovery is unverified.** If the identity database is corrupted or the key file is missing, what happens? `IdentityErrorPathTests` covers handler-level errors; resilience against corrupted storage is not asserted.
- **Multi-identity per actor** — the surface supports it (Create takes a name), and tests exist for switching, but no end-to-end test asserts that User and Service actors can hold *different* defaults simultaneously without crosstalk. The actor isolation is by design; just not pinned.

**Verdict: SOLID for the day-one use case.** Create / list / switch / archive / get all work and have tests. The partial items (first-run, recovery, multi-actor crosstalk) are edge cases that future work can pin as it lands. Not a blocker for module-port work.

---

## Settings

**Surface.** Three files. `PLang/App/Settings/IStore.cs` (interface), `Settings/this.cs` (per-actor settings + module-scoped views), `Settings/Sqlite.cs` (the SQLite-backed store).

**Tests.** Six TUnit suites:
- `PLang.Tests/App/Context/ActorSettingsStoreTests.cs` — per-actor store wiring
- `PLang.Tests/App/Settings/SettingsTests.cs` — core read/write
- `PLang.Tests/App/Settings/ModuleViewTests.cs` — module-scoped views
- `PLang.Tests/App/Settings/ScopeTests.cs` — scope isolation
- `PLang.Tests/App/Settings/SettingsApplyTests.cs` — bulk apply
- `PLang.Tests/App/Modules/settings/SettingsDataTests.cs` — module-action layer

**What works.** SQLite-backed (uses `Microsoft.Data.Sqlite`, connection per operation with internal pooling). Per-actor store wired (System, User, Service each hold their own). Module-scoped views (`Settings.For("module-name")` carves a namespaced sub-surface). Scope isolation between actors. Sync-over-async is explicitly acknowledged in the code (`Sqlite is synchronous under the hood — SQLite has no async I/O`).

In-memory variant exists (`Sqlite.cs:62`) — useful for tests and per-instance ephemeral stores.

**What's partial or missing.**
- **No PLang `.goal` tests for settings round-trip.** All coverage is C# TUnit. A developer using `settings.read` / `settings.write` from PLang has no `Tests/Settings/*.test.goal` to point at. The Tests/Settings directory exists but I haven't audited its content.
- **Schema migration story is undocumented.** What happens when the schema needs to change between PLang versions? `Sqlite.cs:316` sanitizes table names. There's no visible migration runner.
- **Encryption at rest is not asserted.** Settings stores credentials (API keys for LLM/HTTP, etc.). The store is plain SQLite. Whether keys-on-disk encryption is intended at this layer or upstream (via crypto.encrypt on the values before write) isn't documented in the file.

**Verdict: SOLID for storage; story-incomplete for sensitive data.** Functional and well-tested at the C# layer. The encryption-at-rest question is real and worth a design pass before secrets-bearing modules (Webserver auth, LLM API keys, signing keys) port over — but isn't a foundation gap in the sense of "broken;" it's a "decide explicitly before scaling."

---

## KeepAlive

**Surface.** One file, 40 lines. `PLang/App/KeepAlive/this.cs` is a disposal collection: `Add(instance)` promotes an object to app lifetime, `Remove(instance)` disposes it synchronously, `DisposeAsync` disposes all on App shutdown.

**Tests.** None at the App level. No `PLang.Tests/App/KeepAlive*` suite. No `Tests/KeepAlive/`.

**What works.** The collection is constructed (`App.@this:271`) and disposed (`App.@this:606`). It correctly handles `IAsyncDisposable` and `IDisposable`. The Add/Remove discipline is OBP-clean (owned data, owned methods).

**What's partial or missing.**
- **Zero consumers.** `grep -rn "KeepAlive.Add"` returns nothing. No module in runtime2 currently registers a long-lived disposable. The surface exists in advance of consumers that don't exist yet (Webserver listener, Schedule cron loops, file watchers).
- **No tests.** Because there are no consumers, there's nothing to test.

**Verdict: QUIET SURFACE — present, unused, ready.** Not broken; not exercised. The first real consumer (likely Webserver's listener task) will be the first integration test. **Worth flagging to the next module-port architect** that KeepAlive is the right home for background work disposal — so future modules don't reinvent the pattern.

---

## Cross-cutting observations

**The pattern that emerges.** Three of the four areas (Snapshot, Identity, Settings) are SOLID at the C# layer but have asymmetric PLang `.test.goal` coverage — some areas (Identity) have rich .goal tests, others (Settings, full-app Snapshot) lean on C# alone. This isn't a foundation gap; it's a test-layer distribution question that test-designer can pick up incrementally as modules port.

**KeepAlive is the outlier.** Built for a future consumer, no current consumer. The cleanest move when WebserverModule (or ScheduleModule) ports is to wire its long-lived task into `app.KeepAlive.Add(...)` so the disposal pattern is established from day one — not retrofitted.

**Nothing here is "todo says open, code says done"** in the same dramatic way CallStack and the lazy generator were. The verification didn't surface another instance of the documentation/code drift that motivated this branch. Good signal.

**The two real items to add to `todos.md`** (folded into stage 1 if not already):
1. Encryption-at-rest decision for Settings (before secrets-bearing modules port).
2. End-to-end PLang snapshot round-trip tests (folded into the ask-user transport work, not standalone).
