# Auditor — call-cost v1

**Verdict: FAIL (narrow).** One pre-merge item (A1: a red test in the full C# sweep). Everything else is minor and deferred.
**Next bot: coder** — A1 only (a threshold or a measure), then I re-run the sweep.

Scope: runtime2 7bf093245 to e63656a51 (tip; b8c0d4ac9 inside). 91 files; the 78 production source files read whole (I read the diffs of
`call/this.cs`, `type/this.cs`, `actor/setting/this.cs`, `goal/this.cs`, `goal/list/this.cs`, `shortcut/this.cs`, `goal/step/list/this.cs`
myself; two read-only agents read the other 71, marked "agent-read" and verified where ranked). Bench tools and `.pr` files not read for smells.

**Patterns first.** The branch is patterns 11 (made on the first ask: step cache, frame's parameter/property/tags/errors/children, `Broken`), 5 (a birth fact,
not a search: a shortcut's name, `Kinded` made once and kept by the type's list, goal.list's found, the settings answer kept per version) and 12/"no static"
(the type reached by reference). Each lazy field is a `CompareExchange ?? field`: thread-safe on the first ask. Return early: no nested-guard in the touched files
(`json.Stamped`, `text` serializers were rewritten with the guard first).

## Tests (run by me, keys unset, PlangConsole rebuilt)
- C#: Modules 1500, Types 1047, Wire 641, Data 882, Generator 191 — green. **Runtime: 1 red** (A1) of 1502.
- `plang --test`: 323 total, 322 pass, 0 fail, 0 stale, 1 skipped; tree clean.
- Not run: the live llm gate; no mutation tests.

## Previous reviews assessed
- **codeanalyzer (stages through e441cfb40): partial.** Measured every stage; did not see A1 (its run was green), and left the type readers half-moved (B2).
- **tester (v1, v2 a0ca57776): partial.** Pins the lazy births with byte counts — one of them is order-dependent (A1).
- **security (v1 ac2c5c1ad): agree** (no finding Low+); the shared kept settings answer (B1) is the one thing to hand back to it.

## Findings

### A1 — major, pre-merge — review-gap / test: a byte-count pin fails in the full sweep
`PLang.Tests/Runtime/App/Context/BirthTests.cs:178`, `ASubAppsStepCache_IsMadeAtItsFirstAsk`: `born` must be < 40,000 bytes. In the full `./dev.sh test`
it fails every time I ran it (received 40,656 and 40,768; three runs of the class alone pass). The test's own comment says 38,912 "on this machine" —
a 2.7% margin on an allocation count that moves with what the process has already warmed. The gate is red, so the branch cannot merge as is.
Pattern: none (a test, "no runtime code for tests" is untouched). Missed by: codeanalyzer, tester (green runs, other order/machine).
Suggestion: pin what the test means — the cache is NOT made at birth (86,036 with it) — with a threshold between the two (e.g. 60,000) or assert the cache field is null
before the first ask; do not assert a figure the process order can move.

### Minor (deferred; `plan/later.md`)
- **B1** (12, `actor/setting/this.cs:116-135`): a kept answer is worked out by the keeper (the actor's or root scope's context), not the asker's, and one `Data` is
  now shared across concurrent runs; `As<T>` can return the same instance and faces share the attachment, so a handler that sets a property on its parameter writes
  onto the shared answer. The comment says the context is intended; the shared write is untested. Hand to security (decision 23 notes it).
- **B2** (5, type readers): `dict/list/duration/size/clipboard/number` readers' null-row paths (and `type/serializer/Reader.cs:24,47`) still make `list[new type(...), ctx]`;
  the `type` reader's null row finds `list[typeof(type)]`, whose `Types` is null, so `Kinded` falls back to a fresh list-less `{type}` per null row. Two idioms in one file.
- **B3** (12, `type/this.cs:884`, `type/list/this.cs:249`): `Kinded` passes `this` as the entry; called on an already-kinded type it forks the cache (every call site today passes an entry). Use `Family`.
- **B4** (behavior change, `type/kind/this.cs:210`, `path/this.Operations.cs:106`): `list[name]` throws `KeyNotFoundException` on a name no type answers to; the old `new type(name)` path did not. One test or a guard.
- **B5** (`goal/list/this.cs:271-279`): the comment says any writer lets `_found` go; only `Add` does. A plang `remove` on the list (the inherited `Remove`) leaves `_found` — like `_app`/`_system` already — pointing at a removed goal. The `RemoveAt` family is ruled; fix the comment or reset it in the writers.
- **B6** (11, `goal/this.cs` `Broken`/`Changed`): a start that judged before a build wrote a step's Error can publish its stale empty list after `Changed()` cleared it. Needs a build writing step errors while a parallel start of the same goal runs.
- **B7** (nits): the `CompareExchange ?? field` line is copied seven times plus a `LazyInitializer` use (`attachment.cs`) — one idiom, one helper; `call._depth` is an underscore field read from `call/list/this.cs`; `Depth` makes a number per read (decision 5);
  `goal/step/list/this.cs` still mints a `number` for `Child.Count`/`Code.Count` on the build path (boy scout); `Instance(context)` no longer uses `context`; creative locals `read`/`found`; `type.First()` is a creative name; the generator's `__d{Name}/__s{Name}` names can collide for `Foo`/`vFoo`.

Known and ruled, not re-raised: stage 5 dropped; stage 6 held; `error.Type` and operator births with no app; `goal.list`'s inherited `RemoveAt`; a number kind's own `Read` retyped.

## Locked core
`Kinded`, `Made` and number's two ctors are internal (not in `surface_golden.txt`); `context.Json` is covered by summary row 13, `Kinded`/`call.list.Number` by row 3. No unapproved surface change.

---

## Update — A1 and three minors closed at fd91aea2b (PASS)

Delta e63656a51..fd91aea2b (7 files; production: `goal/list/this.cs` comment, `type/kind/this.cs`, `type/this.cs`). Keys unset, PlangConsole rebuilt: C# Modules 1500, Types 1049, Wire 641, Data 882, Generator 191, Runtime 1512 — all green (the Runtime birth pin that failed before passes in the full sweep); `plang --test` 323 / 322 pass / 0 fail / 0 stale / 1 skipped; tree clean. A second `dev.sh test` returned the cached result, so the green is one fresh full sweep plus the coder's two.

- **A1 closed.** Each byte pin's bound now sits between its two measured states (step cache < 60,000 against 86,036 made at birth; the others with a reason each), and the two narrow whole-run pins are split into direct pins at the ask (`Asked(...)` < 24 / 12 / 44 bytes). The figures no longer ride process order.
- **B3 closed.** `Kinded` keys `Made` by `Family`.
- **B4 closed.** `kind.type(context)` answers a reader's unknown name as the list did (`list[new type(name), context]`); the other moved `list[name]` sites name literal types plang holds.
- **B5 closed.** `_found`'s comment now says `Add` is the one writer that lets it go. (The inherited `Remove` still leaves it — the ruled `RemoveAt` family, backlog.)
- Security's probes are merged in as tests (`CallCostSecurityProbeTests`); green.

verdict.json is pass; next bot docs. B1, B2, B6, B7 stay on the backlog.
