# codeanalyzer v1 — app-lowercase

## Summary

18 commits, ~618 .cs files touched, two workstreams: a mechanical `App` → `app`
namespace rename (Phases 1–4 + Builder) and seven OBP merges that collapse
case-pair folders into single namespaces. Build clean (0 errors), C# suite
2752/2752, PLang suite 203 pass / 6 known-fail negative fixtures.

The rename and the merges are both shape-correct on inspection. Findings below
are minor: one missed lowercase folder (`app/data/Code/`), one carve-out worth
documenting (`app/filesystem/Default/`), and ~8 stale docstring references that
still say `App.X` in comments.

No blockers. Sub-merge-ready after the small lowercase miss is addressed.

---

## Pass 1 — OBP compliance

### 1a — Rule-level violations

None found across the 7 merged folders.

Verified for each: `cache`, `builder`, `callback`, `settings`, `code`, `debug`,
`modules`.

- **cache** (`PLang/app/modules/cache/`) — `ICache` + `Memory` + `wrap`. No
  `@this` because `app.@this` holds an `ICache` directly (`PLang/app/this.cs:141`).
  Single owner. ✓
- **builder** (`PLang/app/modules/builder/this.cs`) — build state (`Files`,
  `Cache`, `IsEnabled`, `_prSnapshot`) owned by `@this` with private mutation
  methods (`SnapshotPrFile`, `GetPrSnapshot`). Sibling action handlers
  (`load.cs`, `validate.cs`, `goalsSave.cs`, etc.) call through the @this. ✓
- **callback** (`PLang/app/modules/callback/this.cs`) — thin config container
  composing `Signature.@this` + `Wire.@this`. Single 18-line type. ✓
- **settings** (`PLang/app/modules/settings/this.cs`) — `Get`/`Set` on
  `@this`, persistence via `app.SettingsStore`. ✓
- **code** (`PLang/app/modules/code/this.cs`) — named provider registry, all
  mutation behind generic methods that delegate to a single non-generic
  implementation. ✓
- **debug** (`PLang/app/modules/debug/this.cs`) — single 744-line @this owns
  flags + grep state + formatters + event handlers. Large but cohesive: all
  debug-channel concerns concentrated in one type. ✓
- **modules** (`PLang/app/modules/this.cs`) — registry, discovery via
  generator. ✓

### 1b — Shape smells (yes/no per item)

Ran the four-item checklist against `PLang/app/`.

1. **Public mutable collection with rules enforced from outside?** — **No.**
   Public `List<T>`/`Dictionary<K,V>` properties exist but they are DTO init
   slots (wire types: `AskCallback.cs`, `ErrorCallback.cs`,
   `BuildResponse.cs`, `GoalCall.cs`) or read-only projections (`Events.cs`
   `Before`/`After`, `actions.cs` `Value`). The candidate mutable surfaces
   (`callstack/call/{children,diffs,errors}/this.cs`, `callstack/audit/this.cs`,
   `channels/channel/events/this.cs`) all wrap their state behind `_lock` and
   expose `Add`/`IReadOnlyList<T>` shape — discipline owned by the type.

2. **Cross-file lock target (`lock (other.X)`)?** — **No.** Every `lock (` in
   `PLang/app/` locks a private field of the locking class. Searched:
   `types/choices/this.cs`, `types/Registry.cs`, `callstack/{call/children,
   call/diffs, call/errors, audit}/this.cs`, `channels/channel/events/this.cs`,
   `modules/builder/code/Default.cs`. All clean.

3. **Same logical thing stored twice across types?** — **No, after these
   merges.** The seven merges *resolved* prior smell-#3 violations: each pair
   (`app/Cache/` + `app/modules/cache/`, `app/Settings/` + `app/modules/settings/`,
   etc.) now lives in one namespace. No new instance of smell #3 introduced.

4. **Allocate / mutate / clean-up split across three files?** — **No.** The
   merges collapsed exactly this kind of split. No new instance found.

**Pass 1 verdict: CLEAN.**

---

## Pass 2 — Simplification

### Finding S1 — `app/data/Code/` not lowercased

**Severity:** low. Inconsistency, not a defect.

**Files:**
- `PLang/app/data/Code/Default.cs:4` (`namespace app.data.Code;`)
- `PLang/app/data/Code/IGrep.cs:3` (`namespace app.data.Code;`)
- `PLang/app/data/this.Navigation.cs:171,176,177,179` (consumers)
- `PLang/app/modules/code/this.cs:254` (consumer)

Every other code-provider folder in the tree is lowercase:
`app/modules/{assert,builder,condition,crypto,file,http,identity,llm,signing,ui}/code/`.
`app/data/Code/` is the lone PascalCase folder, holding the grep code provider
(`IGrep` extending `app.modules.code.ICode`).

This is a Phase-4 miss — the architect plan named `Code` in the lowercase list
(plan.md:15) and Phase 4d included Code. The `app/Code/` folder did get merged
into `app/modules/code/`, but the nested `app/data/Code/` was not touched.

**Recommendation:** rename `PLang/app/data/Code/` → `PLang/app/data/code/`,
update the two `namespace` lines and the five consumer references. ~10 lines
of edits, single commit. Leave for coder.

### Finding S2 — `app/filesystem/Default/` carve-out

**Severity:** info, not a defect.

**Files:** 10 files under `PLang/app/filesystem/Default/`.

`Default` stays PascalCase because `default` is a C# reserved word. This is
honest — coder's summary calls it out. The carve-out *is* an exception to the
"lowercase = PLang vocabulary" rule, since "the default filesystem" is plang
vocabulary.

The convention is currently encoded only in coder's summary
(`.bot/app-lowercase/coder/summary.md`) and the claude-md-proposals file.
Worth documenting in the repo CLAUDE.md alongside the lowercase rule, so future
sweeps don't trip over it again.

**Recommendation:** make the rule visible in CLAUDE.md. Coder already proposed
similar text in `.bot/app-lowercase/claude-md-proposals.md` — extend the
proposal to call out `Default` as the only PascalCase carve-out and explain
why. Docs bot at merge.

### Finding S3 — Breaking plang API renames are placeholders

**Severity:** low. Naming follow-up, not blocking merge.

**Files:**
- `PLang/app/modules/environment/run.cs` (was `app/modules/app/`)
- `PLang/app/modules/builder/load.cs` (was `builder/app.cs`)

Coder flagged both as renamed under pressure. After reading:

- `environment.run` — handler description "Run a goal, step, or action on a
  specified actor". `environment` reads as "the env you're running in";
  acceptable. The PLang surface is `environment.run goal="X"`. Not great, not
  bad.
- `builder.load` — loads the app-level build context from a directory.
  `builder.load path="."` reads naturally. Probably fine to keep.

Neither is broken. Both should get a deliberate naming pass before the next
release goes out the door (Ingi's call), but they don't block merging this
branch into `runtime2`.

---

## Pass 3 — Readability

### Finding R1 — Stale `App.X` docstring references (~8 sites)

**Severity:** low. Doc drift, no runtime impact.

| File | Line | Stale text |
|---|---|---|
| `PLang/app/data/this.cs` | 554 | `// Raw-name carve-out: types like App.Variables.Variable` |
| `PLang/app/GlobalUsings.cs` | 64 | `// App.modules.goal.Call (the goal.call action handler)` |
| `PLang/app/channels/channel/events/this.cs` | 10 | `Same shape spirit as <c>App.Goals.Goal.Events</c>` |
| `PLang/app/types/Registry.cs` | 39 | `e.g. <c>App.Goals.Goal.GoalCall</c>` |
| `PLang/app/callstack/call/Position.cs` | 8 | `in the live App.Goals registry` |
| `PLang/app/modules/settings/IStore.cs` | 63 | `e.g., App.modules.encryption → "encryption"` |
| `PLang/app/errors/CallbackGoalErrors.cs` | 27 | `the live <c>App.Goals</c> registry` |
| `PLang.Generators/Discovery/this.cs` | 41 | `Variable-name slots use Data&lt;Variable&gt; (App.Variables.Variable)` |

These are docstrings/comments only. Property-position references like
`ctx.App.Goals.Get(...)` are correct (property names stay PascalCase). The
docstrings should match — sed pass with manual review.

**Recommendation:** one cleanup commit, ~8 single-line edits. Leave for coder
or docs.

### Finding R2 — `PLang.Tests/GlobalUsings.cs` line 64 typo

**Severity:** trivial.

`PLang.Tests/GlobalUsings.cs:64`:
```
// Call: not a global alias — App.modules.goal.Call (the goal.call action handler)
```

Should be `app.modules.goal.Call`. Same class as R1 above.

### Finding R3 — `PLang.Tests/GlobalUsings.cs` not retired

**Severity:** info, expected.

Architect plan said the `GlobalUsings` aliases "stop being load-bearing" after
the rename. They are not load-bearing now (no BCL shadowing) but they remain
in place as convenience aliases (`Data`, `Variables`, `Step`, etc.) because
`app.data.@this` is awkward to type at use sites.

This is a deliberate carry-over, not a finding. Worth noting only because the
architect plan suggested they could be dropped — the reality is they read
better than `@this`-suffixed type references and stayed.

---

## Pass 4 — Behavioral

### String-literal trail

Searched for `"App."` and `"app."` string literals in production code and
generator.

- `PLang.Generators/Discovery/this.cs:59,65,134,155,175,183,204` — all
  lowercase (`"app.modules"`, `"app.data"`, `"app.variables"`). ✓
- `PLang.Generators/Emission/Action/this.cs:176` — `const string prefix = "app.modules.";` ✓
- `PLang/app/modules/this.cs:33,62` — `Discover(typeof(@this).Assembly, "app.modules");` ✓
- `PLang/app/modules/builder/this.cs:63` — `ns.StartsWith("app.goals", ...)` ✓ (the
  StoreOnlyModifier fix — confirmed lowercased).

No stale `"App."` string literals found in production code or the generator.

### Case-collision audit

```
find PLang/app -type d | awk -F/ '{print tolower($0)}' | sort | uniq -d
```

Empty result — no case-only folder collisions remain anywhere under
`PLang/app/`. ✓

### Cross-platform safety

The branch is clone-safe on case-insensitive filesystems (Windows/macOS-default).
The seven OBP merges removed the case-pair traps that would have broken those
clones.

---

## Pass 5 — Deletion test

- No empty `this.cs` files found.
- No 0-byte source files.
- No tombstone "removed" comment-only files.
- No leftover top-level PascalCase folders that the merges should have
  consumed (`Cache`, `Builder`, `Callback`, `Settings`, `Code`, `Debug`,
  `Modules` all gone from `app/` root).

---

## Build + test verification

Clean rebuild per CLAUDE.md "stale-binary trap" protocol:

```
rm -rf */bin */obj
dotnet build PlangConsole   # 0 errors, 447 warnings (pre-existing nullable warnings)
dotnet run --project PLang.Tests   # 2752 pass / 0 fail
cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test
   # 203 pass / 6 fail (intentional negative fixtures: sensitivefail × 4 across
   # discoveries, failsvar × 2 — matches runtime2 baseline)
```

Both gates green.

---

## Verdict

**PASS.**

Three follow-up items for the coder, none blocking:

1. **S1**: rename `app/data/Code/` → `app/data/code/` (one folder + ~6 references).
2. **R1**: scrub the 8 stale `App.X` docstrings to lowercase.
3. **S2**: extend the claude-md-proposal to document the `Default` PascalCase
   carve-out under the lowercase rule.

S3 (`environment.run` / `builder.load` names) is a naming-policy decision for
Ingi, not a defect.
