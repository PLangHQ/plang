# OBP audit — App root sweep

**Author:** architect. **For:** coder. **Status:** plan, no code written. You own the final shape.

The crawl from `PLang/app/` (root) measured every subtree against the formal OBP rules (`Documentation/v0.2/object_pattern_formal.md`) + the smell checklist, cross-checked with `tools/ObpScan`. The core is well-shaped (callstack, the event/channel collections, most leaf handlers, the convert/compare/kind registries are textbook). The breaks cluster at specific seams, captured below as four stages.

## The philosophy this serves — zero overhead per method

OBP's payoff is that reaching a thing costs *only* the navigation. `app.goal.step.action` = four reference hits, landing on the class that owns "what an action is" and answers which one (the current one, by its own logic). No repository to `new`, no service/manager layer, no filter pass, no unit tests for the plumbing — because there's no plumbing. The class IS the logic.

Every finding here is the same defect in a different costume: **a method or call site doing work the owner should own** — a type-switch, a stringify, a hand-newed element, a by-name fish, a projection baked where the caller should select. Each one is overhead: execution cost *and* test cost (you test the choreography instead of trusting the navigation). The fix is always "push it onto the owner; let the caller navigate."

Two naming laws this audit enforces (both yours, both structural — no exceptions):
- **Singular = the class = one instance.** `app.goal` is *a* goal (the one in play); `app.goal.list` is the goals. A plural folder/namespace name lies. There is no "real module name" carve-out.
- **No verb+noun on public members** (only pure `IsXxx`/`HasXxx` bool). Want the names of the channels? Navigate to `app.channel.list` and the caller projects names. The owner exposes the collection; it does not pre-bake views of it. Single verbs that do real work (`Resolve`, `Open`, `Covers`, `Read`) are fine.

---

# Stage 1 — singularize the goal entity chain

**Goal:** finish the de-pluralization. Three plural namespaces survive, all in the goal chain.

| Now | → | One instance (`app.X` / `.current`) | Collection (`.list`) |
|---|---|---|---|
| `app.goal.steps` | `app.goal.step` | the current step (class decides — `CallStack.Current…Step`) | `app.goal.step.list` |
| `app.goal.steps.step.actions` | `app.goal.step.action` | the current action | `app.goal.step.action.list` |
| `…action.modifiers` | `…action.modifier` | a modifier | `…action.modifier.list` |

**Shape:** the collection types already exist — `steps.@this : IList<Step>` (`goal/steps/this.cs:8`), `actions.@this : IList<action.@this>` (`goal/steps/step/actions/this.cs:5`), `modifiers.@this : IList<PrAction>` (`…/modifiers/this.cs:11`). The rename folds the doubled `steps/step` and `actions/action` folders into `step` + `step/list` and `action` + `action/list`, matching the singular-with-`.list` convention the rest of `app/` already follows. The doubled `@this.Value : List<Step>` (`steps/this.cs:20`) and `List<action.@this>` (`actions/this.cs:42`) become `step.list` / `action.list` streaming their element.

**Blast radius:** ~80 files (the deepest namespace in the codebase — `steps.step.actions.action` is the entity spine). Pure internal C#: **zero** `.goal` / `.pr.json` references it. Mechanical but wide — one atomic sweep; it will not compile half-done.

---

# Stage 2 — `settings` → `setting`

**Goal:** the last plural, but a different kind — it's a developer-facing module name.

`app.module.settings` → `app.module.setting`. One instance is *a* setting (`setting["key"]`); all of them are `setting.list`. The PLang action `settings.get`/`settings.set`/`settings.remove` becomes `setting.get`/`setting.set`/`setting.remove`.

**Blast radius (small, today):** 0 `.goal` files, 0 built `.pr.json`, 11 C# sites reference the namespace. But it crosses the PLang surface, so it's a *different kind* of change than Stage 1 — flag docs/builder. Also: `settings` has no `.list` collection type yet (`settings.cs` is the store), so this is "rename **+ introduce `setting.list`**," not just a rename. Decide the `setting.list` shape (the store's keys/entries as a collection).

---

# Stage 3 — Errors / Warnings / Parameters (the design redesign)

**Goal:** three lists left raw on entities, each a different OBP question. Not a mechanical wrap.

## 3a. Parameters → `parameter.list` (the doc was wrong)

`Action.Parameters : List<Data>` is read by name 22× via `FirstOrDefault(p => p.Name == …)` — the recurring fish smell (same family as the worklist Part 2). The domain op is real: **select-a-parameter-by-name**, reimplemented 22 times. A `parameter.list` collection with `["name"]` / `.Get(name)` kills all 22 sites.

The OBP formal doc documents `.Parameters — List<Data>` (line 353) — **that decision is retired** (confirmed). Update the doc when this lands. `Action.Return` (`List<Data>?`) is the same shape — fold it in (`return.list` or reuse `parameter.list`).

## The governing principle for 3b/3c — *value fixed by type ⇒ it's the type*

If a property's value is determined by the object's type (not its instance), it is not a property — it is the type. `Key`/`Code`/`Message` vary per instance → real fields. `Severity` (error vs warning) and `Category` (application vs runtime) are fixed by *which kind* the thing is → they must be the type, never a flag enum. A flag that re-encodes type identity is the inverted type-switch smell: every element redundantly declares its own kind, and a second derive (or a `switch`) inevitably drifts from it.

**Proof this is real, not theoretical — `ErrorCategory` today:** the name is noun+noun, owner-prefixed (`Error`Category, in `app.error`) — the flashing sign. 8 error types each `override Category => ErrorCategory.X` restating what their type already says; the base *derives* it from `StatusCode < 500` (second source of truth → drift); **zero logic reads it** — only 2 serializers stringify it (`IError.Wire.cs:47`, `error/serializer/Default.cs:31`). It is pure ceremony re-encoding type identity. **Delete it** — the enum, the 8 overrides, the base derive. The wire already carries the `[type]`; if "developer-caused vs system" is ever needed it's a virtual `bool` on the type, computed once — but nothing reads it, so YAGNI.

## 3b. Errors + Warnings → one `diagnostic` family (severity is the type, not a field)

**The smell isn't "raw list" — it's two lists distinguished by what should be the element's type.** `Errors` and `Warnings` are both `List<Info>` on Goal, Step, Action (`goal/this.cs:180-183`, `step/this.cs:103-106`, `action/this.cs:46-49`) — severity encoded by *which list you're in*. That's "same logical thing stored twice," and the fix is NOT to add a `Severity` enum (that would rebirth the `ErrorCategory` mistake — see the principle above).

The family — `Info` → `Diagnostic`, severity carried by the type:
```
Diagnostic { Key, Code, Message }          // base — NO Severity field
  ├─ warning : Diagnostic                   // plain build diagnostic
  └─ error   : Diagnostic                   // + the rich runtime fields (see 3c)
```
- `Code` is the diagnostic code (PLNG001-style); `Key`/`Message` per-instance. No flag fields.
- One shared collection type `diagnostic.list.@this`, instantiated per owner: `goal.diagnostic` / `step.diagnostic` / `action.diagnostic` (build), `app.diagnostic` (runtime, see 3c).
- `is error` / `is warning` answers severity — a type test, not a field read. `app.diagnostic.error` selects the `error`-typed members; `.warning` the warnings; `.list` all.

**The cross-file `.Add` fix — domain verbs that name the type they construct.** Today the builder reaches in and news up the element:
```csharp
a.Warnings.Add(new Info { Key = "...", Message = "..." });   // builder knows the shape + does the append
```
becomes:
```csharp
a.diagnostic.warn(code, message);     // constructs a `warning`, appends
a.diagnostic.error(code, message);    // constructs an `error`, appends
```
The verb names the type it makes; the collection owns construction + append; the builder states intent and never sees the element shape or calls `.Add`. Only 6 `new Info` sites today — small migration. Reads (`from.Errors.Count > 0` at `step/this.cs:229,235`) become `from.diagnostic.error.list.Any()` (select-by-type) or a `.HasErrors` bool.

Also covers: `data/this.Result.cs:61 Warnings`, `BuildResponse.cs:13-14 Errors/Warnings` — same collapse.

## 3c. `error` IS-A `Diagnostic` — `app.diagnostic` / `app.error`

The runtime error unifies into the same family: `error : Diagnostic`, carrying the rich runtime fields the base doesn't — `Id, StatusCode, FixSuggestion, HelpfulLinks, CreatedUtc, Exception, ErrorChain, Step, Goal, CallFrames, Format()`. The existing 10 `: Error` subtypes (`ServiceError`, `ValidationError`, `AskError`, …) ride along as `error` subtypes unchanged. `IError` becomes effectively `IDiagnostic` of the error kind.

This makes `app.diagnostic.add(error)` type-check (an error IS-A diagnostic), and resolves the naming collision by unification rather than separation:
- `app.diagnostic` — the runtime stack (replaces today's `error.list` Trail, `error/list/this.cs:41`). `.add(d)` appends.
- `app.diagnostic.error` — the current error (class decides — top of trail), typed back to `error`. `app.error` aliases it. `app.error.list` → `app.diagnostic.error.list`.

Reconciliations settled: `Code` (string, base — the rule/diagnostic code) and `StatusCode` (int, error-only — HTTP) stay distinct. `Severity` is **not** added (it's the type). `Category` (`ErrorCategory`) is **deleted** (see the principle).

Cost to call out: this is the heaviest item in Stage 3 — `Error.cs` + `IError` + the 10 subtypes move under the `Diagnostic` family. Worth it for the single family and the deleted ceremony, but it's a real refactor, not a rename.

---

# Stage 4 — annotated OBP documentation pass (executes AFTER stages 1–3)

**Goal:** for every finding section below, paste the *moved/renamed* source into this doc and annotate it: how it fits or breaks OBP, and how to adjust / move / reduce it — driving toward zero overhead per method. **Runs last** because stages 1–3 move the code; documenting pre-move source would be thrown away.

## The annotation format (one block per section)

````markdown
### <area> — <one-line OBP verdict>
`file:line`

```csharp
<the actual source, as it stands post-move>
```

**Fits OBP:** <what's already right — navigate-don't-construct, owner-owns-behavior, …>
**Breaks OBP:** <which rule/smell #, the specific misplacement>
**Adjust → :** <the moved/reduced shape; whose member it becomes>
**Overhead removed:** <the choreography/tests that disappear>
````

## The sections to document (the behavioral findings — verified against current code)

These are the non-rename findings the crawl surfaced. Stage 4 writes each up in the format above; the verdict + fix is pre-stated here so the coder isn't deriving from scratch.

**Tier 1 — behavior parked off the value (highest leverage):**
- **`data/this.Navigation.cs:147` `Data.InvokeMethod`** — a `method.ToLowerInvariant() switch` does `tolower`/`toupper`/`trim`/`replace` by stringifying `Peek().ToString()`. But `text.@this` already owns `Upper()/Lower()/Trim()/Replace()` (`text/this.cs:158-160,203`). The courier (Data) is doing the leaf's (value's) job; a `date`/`dict` reaching `trim()` is silently coerced to string instead of declining. **Fix:** an `item.@this.Invoke(method,args)` virtual (sibling to `Navigate`); each type answers or declines; delete the Data-side switch. *Biggest single fix.*
- **`type/file/this.cs:112` + `directory/this.cs:75` `IsTruthy()`** — `Path is path.file.@this fp && fp.Exists` downcasts to one scheme; an http-backed file silently answers false. `image` does it right (async `AsBooleanAsync` via abstract `Path.ExistsAsync()`). Same downcast at `file.Size` (`:139` → 0 for non-FilePath). **Fix:** route through the abstract path; drop the `is FilePath`.
- **`signing/code/Ed25519.cs:83-140`** — `VerifyAsync` inlines freshness (`now - Created`), expiry (`now > Expires`), nonce-replay, `ContractsMatch`; only the byte verify is algorithm-specific. The `signature` value owns `Nonce/Created/Expires/ContractStrings` (`signature/this.cs:46-52`) but not its own validity. A second `ISigning` re-implements all of it. **Fix:** `signature.IsExpired(now)` / `IsFresh(...)` / `Covers(contracts)`; the algorithm only does `Verify(bytes, sig, identity)`.

**Tier 2 — verb+noun projections (the `ChannelNames` family — caller should navigate + select):**
- `module/this.cs:228 GetChannelInventory()` → delete; caller uses `actor.channel.list`.
- `channel/list/this.cs:152 ChannelNames` → delete; caller projects from `list`.
- `variable/list/this.cs:747 GetNames()`, `:680 GetChangedSince()` → same; expose the collection.
- `type/catalog/this.cs:461 GetValidValues()` twins a `ValidValues` member (H2 Get-twin) → one of them.
- `actor/context/this.cs:439 GetEventBindings()`, `event/list/this.cs:98 GetBindings()` / `:109 GetMatchingBindings()` → the binding collection IS the API; the caller selects matches (this is also the `LifecycleFor` finding — the fix is NOT `LifecycleFor(owner)`, which is itself verb+noun).
- (Carve-outs, NOT findings: the `path` verb surface `ReadText`/`WriteText`/`ReadBytes` is sanctioned; `ExamplesForLlm()`/`Choices()` are LLM-catalog projections; `*Async` I/O leaves.)

**Tier 3 — a concept with no owning collection:**
- **mock** — `reset.cs:22` reconstructs "tear down all mocks" by scanning the *event* registry (`GetBindings(BeforeAction)` + `OfType<mock>`); mock identity smuggled via `binding/this.cs:36 Targets : List<object>`; `mock.Calls` (`mock/this.cs:15`) cleared cross-file. **Fix:** a real `mock.list.@this` owning register/reset/record; drop the `Targets` back-channel.
- **`actor/context/this.cs:371-432 LifecycleFor(Goal/Step/Action)`** decomposes the owner into name-strings 4× and fans out `GetMatchingBindings` per trigger. "Which bindings match this owner" is the binding collection's job, taking the entity. **Fix:** the match lives on the binding collection; pass the owner, not extracted strings.

**Tier 4 — god-methods / decompose-then-rebuild (document + propose split):**
- `llm/code/OpenAi.cs:61-529` — `Query` ~470-line god-method (config + cache + tool-loop + cost + validation + streaming). Extract `Usage`/cost as a value; make the cache entry a self-round-tripping type; leave `Query` as orchestration.
- `http/code/Default.cs:600 BuildProperties` — stamps a 12-key flat metadata bag into `data.Properties` from ~8 call sites; no `http.response` type owns "what a response exposes." **Fix:** an `http.response` value that projects itself.
- `crypto/code/Default.cs:84 Verify` fabricates a `Hash` action and re-dispatches to reuse the algorithm switch → digest belongs on `crypto.type.hash`.
- `identity/code/Default.cs:320 ConvertToIdentity` 4-branch deserializer + the single-default invariant duplicated across Create/SetDefault → `Identity.FromStored` + model the set so "set default" clears others as one op.

**Tier 5 — reference + flat copy / shared-entity mutation (lower, partly intentional):**
- `error/Error.cs:101 Variables : Dictionary<string,string>` — a flat copy of variable state also reachable via `Context.Variable` (can drift). `Format()` already prefers the live view (`:352`). Drop the flat dict; render from the live source / the throw-time `CallFrames` snapshot.
- `error/handle.cs:172` mutates `action.Step` from outside (shared-entity write) → pass the enclosing step through context.
- `data/this.Transport.cs` parks compress/decompress on Data (self-acknowledged TODO) → an `archive` layer owns it.
- `goal/list/this.cs` hand-syncs three parallel indexes (`_goals`/`_byPath`/`_byName`) → one index type owning all keyings atomically.

## Calibration (what is NOT a finding — don't churn)

Verified false positives from the sweep: handlers calling `Context.Events.Register` / `actor.Channel.Register` are correct (those registries own their collections); `assert` correctly delegates to `data.Compare`; leaf handlers reading their own `.Value` is allowed (only couriers are barred); the H3 `Errors`/`Warnings`/`Parameters` bags are handled by Stage 3, not flagged individually; `callstack/call/tag` already peels at its own dict line. The registries are real registries, not proxies (except the two Tier-2 cases noted).

---

## Sequencing

1. **Stage 1** (goal-chain singularize) — atomic, internal, ~80 files.
2. **Stage 2** (settings→setting) — smaller, crosses PLang surface; introduces `setting.list`.
3. **Stage 3** (`diagnostic` family + `parameter.list` + `app.error`) — the redesign. Design settled: severity-is-the-type (no enum), `Info`→`Diagnostic`, `error : Diagnostic`, `ErrorCategory` deleted. Heaviest item (Error.cs + IError + 10 subtypes move).
4. **Stage 4** (annotated documentation) — last, over the moved code.

Pre-1.0: no shims, no flags. Change the shape, fix the callers, update the OBP doc where it's now wrong (3a).
