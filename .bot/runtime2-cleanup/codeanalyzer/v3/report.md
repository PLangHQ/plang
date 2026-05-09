# codeanalyzer v3 — runtime2-cleanup final pass

## What's under review

The full branch `runtime2-cleanup` against its merge target `runtime2`. 27 stages, 106 commits, 218 production files touched. v1 covered stage 1; v2 covered stage 2. Stages 3–27 (25 stages) had no per-stage codeanalyzer review, so this v3 is a cumulative pass focused on the Tier 5 closer (stages 23–27) and an OBP-shape sniff over the App spine.

## Method

Three parallel surveys + independent verification:

1. **OBP smell sweep on the App spine** — `App/this.cs`, `Channels/this.cs`, `Modules/this.cs`, `Errors/this.cs`, `KeepAlive/this.cs`, `CallStack/this.cs`. Run the 4-question CLAUDE.md checklist on each.
2. **Tier 5 deep dive** — every file touched by stages 23–27, with extra scrutiny on the static-vs-instance judgment calls flagged in `architect/results.md` deviations 11–14.
3. **Cross-cutting cleanup verification** — banned patterns (`Console.*` writes), stale `// Stage N:` comments, residual `IProvider` / `App.Build.` / `app.Variables` references, orphan files, deleted folders, TODO markers added during cleanup.

Plus: clean rebuild, both test suites.

## Build & tests

```
$ rm -rf */bin */obj && dotnet build PlangConsole       → green
$ dotnet run --project PLang.Tests                       → 2752/2752 ✅
$ cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test
                                                          → 199/199 ✅
```

(The `[Fail]` lines for `_fixtures_sensitive/sensitivefail.fixture.goal` and `_fixtures_fail/failsvar.fixture.goal` are expected — those are *fixtures consumed by Tester self-tests* that assert "a failing test correctly reports as failed." Final summary line is `199 total, 199 pass, 0 fail`.)

## Findings

### v3-1 — `Types/Registry.cs:34`: public mutable `Assemblies` list (OBP Q1)

```csharp
public List<Assembly> Assemblies { get; } = new() { typeof(@this).Assembly };
```

Read under `_initLock` inside `EnsureInitialized` (line 116). The public surface allows external mutation with no Add/Remove discipline on Registry — exactly the shape stages 16/24/25/26 spent five commits killing. After `_initialized` flips, mutations are silently ignored.

Production grep: zero external mutators today. So it's latent surface, not a current bug. Right shape: private `_assemblies` + `IReadOnlyList<Assembly> Assemblies` + `RegisterAssembly(Assembly)` returning false post-init.

**Severity: non-blocker.** Ironic in a cleanup branch but doesn't break anything in flight. Easy follow-up.

### v3-2 — `modules/test/report.cs:38`: `Console.Out.Write` bypasses Channels

```csharp
var console = new StringBuilder();
RenderConsole(console, results, testing);
RenderCoverageTables(console, testing, Context.App.Modules);
Console.Out.Write(console.ToString());
```

The exact discipline the cleanup is enforcing — and the Tester report module breaks it. `Run()` is already `async` and `Context.App` is in scope; the write should route through:

```csharp
await Context.App.CurrentActor.Channels.WriteTextAsync(
    global::App.Channels.@this.Output, console.ToString());
```

Effect today: tester output is unredirectable; channel transcripts of `--test` runs miss the report block. Not a behavioural regression *introduced* by this branch (the line predates it), but the cleanup didn't catch it.

**Severity: non-blocker.** Single-line fix. Worth taking before merge to keep the branch internally consistent with its own thesis.

### v3-3 — `App/this.cs:347–355`: `Console.OpenStandardOutput/Error/Input` in `WireDefaultConsoleChannels`

```csharp
actor.Channels.Register(new global::App.Channels.Channel.Stream.@this(
    global::App.Channels.@this.Output, Console.OpenStandardOutput(),
    global::App.Channels.Channel.ChannelDirection.Output, ownsStream: false));
// ... same for Error, Input
```

Not a write — it's *acquiring* the OS streams and feeding them into the channel registry. This is *how* channels get their default backing. CLAUDE.md's "Permitted exceptions" enumerates `IsInputRedirected`/`IsOutputRedirected` and `PlangConsole/Program.cs:26`, but doesn't explicitly bless the channel-bootstrap path — yet without it, the entire channel abstraction has nothing to hold.

**Severity: nit / docs.** Code is correct. CLAUDE.md should add this case to the exception list so future grep-driven sweeps don't false-flag it.

## v1/v2 carryover

| # | finding | status |
|---|---------|--------|
| v1-1 | Snapshot back-ref aliasing | Latent, untouched. Out of scope for this branch. |
| v1-2 | Stale `// Stage 1: ctor no longer opens console streams. Entry point wires (Stage 6).` at `Channels/this.cs:61` | **Still present.** Migration-history comment of zero value to a future reader. Trivial. |
| v1-3 | `AppThis_SerializersExists_PerActor` under-asserts distinctness | Out of scope. |

None re-introduced.

## What's clean

The cumulative cleanup landed substantively well:

- **App spine smells (4-question checklist)**: `App/this.cs`, `Channels/this.cs`, `Errors/this.cs`, `KeepAlive/this.cs`, `CallStack/this.cs`, `Modules/this.cs` all clean. Modules.App back-ref earns its keep — actually read at lines 254 & 406 for richer schema rendering.
- **Tier 5 judgment calls (deviations #11–#14)**:
  - `Diagnostics/this.cs` as static class (deviation #13) — verified pure-logic + stateless config bag, Rule C exception applies cleanly.
  - `Types/Conversion.cs` keeping public methods static (deviation #14) — internally consistent with stage 26's static/instance split (state-touching = instance, pure logic = static).
  - `http/Default.cs` static-to-instance conversion (deviation #11) — no lingering static reads of the now-instance options; the larger surface caught by coder mid-flight is closed cleanly.
  - `Callback/Wire/this.cs` (stage 24) — both AskCallback and ErrorCallback wire methods route through `ctx.App.Callback.Wire.Options`. Single home, no duplication.
- **Cross-cutting cleanup**: zero `IProvider` references in App; zero `App.Build.` / `using App.Build` leftovers; zero `app.Variables`/`app.Context` shortcut usages outside the documented removal-comment in `Variables/this.cs:43`; zero `App.Utils.{Json,TypeConverter,TypeMapping,PlangTypeIndex}` in production (only the test facade preserves the legacy namespace, as designed). `App/Utils/` is *exactly* the planned 4 files. All renamed-away folders (`Build/`, `Test/`, `Providers/`, `Choices/`, `Data/Navigators/`, `Data/Providers/`, per-module `providers/`) are gone.
- **No `[Obsolete]` cruft, no orphaned TODO/FIXME/HACK markers introduced by the cleanup.** The 4 TODOs that exist are pre-existing, unrelated.

## Verdict

**PASS.** The branch is in shape to merge to `runtime2`.

Three findings noted; none are blockers:

- **v3-1** (Registry.Assemblies public mutable) — latent surface bug, follow-up.
- **v3-2** (Console.Out.Write in test/report.cs) — single-line channel-discipline fix worth taking; arguably *should* be done before merge to keep the branch consistent with its own thesis. Coder/Ingi judgment.
- **v3-3** (Console.OpenStandard* in App.WireDefaultConsoleChannels) — code correct, docs only.

The architect's self-audit (`results.md`) is accurate. The deviations from the destination tree are all OBP-defensible. Tier 5 closes the static-eviction tail and Utils/ empty-out cleanly.

## Files

- `report.md` — this file.
- `verdict.json` — `{ status: "pass", findings: 3 non-blocker }`.
- `plan.md` — review approach.
