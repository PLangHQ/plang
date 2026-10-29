# Auditor — snapshot, v1 (7ae8f6dd9 and the architect's 340 tip cc944966a), against runtime2 (merge-base b283811f6)

**Verdict: FAIL, narrow. Next: coder** — one major (A1, a silent drop), two pre-merge cheap items (A2 teaching, A3 banned names). Everything else is minor/nit and can ride to a later branch at the architect's call.

## What I ran (OPENAI_API_KEY and TYPESAFE_API_KEY unset)
- `./dev.sh build`, then `./dev.sh test`: Modules 1480, Types 1037, Wire 641, Data 871, Generator 191, Runtime 1424 — **all green, 0 red** (the runtime2 baseline's 6 Wire reds are green here).
- `cd test && ../PlangConsole/bin/Debug/net10.0/plang --test` after the rebuild: **312 total, 311 pass, 0 fail, 0 stale, 1 skipped**; `git status` clean afterwards.
- Read whole: `module/identity/code/Default.cs`, `module/snapshot/resume.cs`, `event/binding/action/this.cs`, `snapshot/this.Variables.cs`, `View.cs` 140–165. Four read-only agents read the other touched files whole (172 files); I re-read the lines of every finding marked *verified*; the rest are marked *agent-read*.

## Previous reviews assessed
- **codeanalyzer** — partial. PASS at 28abb0bbe (architect row 326) fixed the one blocker (the per-push `action.Index` search). Its findings 2 (View.cs:239 names five types in the view) and 3 (late stamp `Under`, `call/this.cs:203`) are still in the code and were ruled "low". The files `codeanalyzer/v3/verdict.json` and `review.md` still say FAIL at 1268c81f7 — the PASS lives only in a bench commit message and the log. It did not catch A1, A3, B2.
- **tester** — partial. v6 result-2: false greens fixed and mutation-checked, flake found. The flake's cause (identity race) was fixed and probed afterwards, but `tester/v6/verdict.json` still reads FAIL and the tester did not re-run after 818ffe898 / the identity gate. I re-ran the suites myself: green. No test touches an `on.event` call with `Actor`/`Parallel` (A1).
- **security** — agree. SAFE, 11/11 + the identity-gate probes; the Low writable members (error, llm message, screen, terminal process) are pinned in WritableTests and left "for later" (commit 5bcab7dea).

## Locked surface
Every re-pin of `surface_golden.txt` names its decision in its commit body (27 commits; spot-checked against the architect log: 2/4, 39, 58–67, 81–86, 94–95, 103, 111–117, 153, 183–200, 216–220, 238–251, 281, 294, 304, 307 all carry Ingi's yes or a ruling). Nothing unapproved found. One note: **`app.this.Start(goal, context, ct)` was removed** under "143 (review 31)" — a codeanalyzer finding, not an Ingi yes. It is a dead overload and the removal is behaviour-neutral, but the locked rule says `app`'s members don't change without his say. (nit, N1)

## Findings

### A1 — major — event/binding/action/this.cs:55-59 — *verified* — missed_by: codeanalyzer, tester — disposition: pre-merge
`Handle` no longer runs the authored `goal.call`; it builds a new `goal.call` with only `Name` and `Parameter`. `goal.call`'s `Actor` (`module/goal/call.cs:28`) and `Parallel` (`:34`) are silently dropped. Before the branch `_call.Start(context)` honoured them. So `on.event … call X on system` / `call X in parallel` now runs on the wrong actor, synchronously, with no error. No test covers it and `os/system/modules/on/event.notes.md` doesn't say the call is narrowed.
Fix: carry every parameter of the authored call across (or run the authored call with the goal found from where it was bound), or refuse `Actor`/`Parallel` with an error naming them; add a test. Same method also: `call["name"]` presence is taken as "is a goal call" (line 44) — test the module/action instead (minor).

### A2 — minor, pre-merge (cheap) — os/system/modules/snapshot/save.description.md:1, resume.description.md:1, resume.examples.md — *verified*
Teaching still says `save %snapshot% to 'run.snapshot'` and `read 'x.snapshot' into %snap% as snapshot`, "restore its captured Variables/CallStack". Only a signed `.plang` file resumes (`SnapshotUnsigned`); `resume` has refusals and `Parallel` the teaching never mentions. A builder following the teaching writes a step that is refused. Teaching edit ⇒ re-run the decider fixtures and page goldens and re-pin.

### A3 — minor, pre-merge — banned/creative names introduced by the branch — *verified by scan of added lines*
`held`: `module/identity/code/Default.cs:100,124,204` (and method `Held` at 312/374, `made` 318, `making` 380); `call/action/this.cs:29`; `goal/step/list/this.cs:30`. `own`: `module/snapshot/resume.cs:69`; `type/item/variable/this.cs:138` (`own`). Others agent-read: `born` (`View.cs:157`), `kept`/`Kept<T>` (`event/on/this.cs:155`, `serializer/Snapshot.cs:24`, `binding/action:95`), `taken`, `face` (`code/kind/this.cs:33,42`), `named`/`written`/`bound`/`calls` (`binding/action:40–55`). Older, in touched files (boy scout): `Held()` in `data/this.cs:711`, `Operator.cs`, `type/list/this.cs:75`, `type/this.cs:284`, `variable/this.cs:113`; `incoming` (`goal/step/this.Item.cs:41`), `declared` param (`data/this.cs:964`), `__held` in the generator (`Emission/Action/this.cs:407`), `Literal.Entries` (`formal/reader.cs`).

### B1 — minor — identity/code/Default.cs:96-106 — *verified* — missed_by: security, codeanalyzer
`Archive` checks `IsDefault && !Force` before the gate and does not repeat it inside `Change`. A concurrent `set default` between check and write archives the default without `force`. Same family as the race the gate was built for; the other four verbs re-check inside. (Create/SetDefault also run their checks twice and ask consent against the stale read — agent-read.)

### B2 — minor — snapshot/this.Variables.cs:17 + View.cs:158 — *verified*
`Get` returns a `ValueTask<data>` but `View.Make` throws `AppException` when a birth declines (and slot readers throw `DeclinedException`). `%snap.app…%` can throw out of a navigation instead of answering the Error. Catch and answer `ex.Error`, or give `Make` an answering form.

### B3 — minor — snapshot/resume.cs:61-70 — *verified*
`Refusal()` calls `GetOrCreateDefaultAsync`: checking a foreign snapshot in an app with no identity creates and stores one (a check with a write). Ask a read-only "is one of the app's identities" first.

### B4 — minor — still-open codeanalyzer 2/3 and friends — *verified*
`View.cs:239` names `"text" or "number" or "bool" or "dict" or "list"` in the view (fork; a virtual on the type belongs there). `call/this.cs:203 Under` + non-readonly `_caller/_previousCurrent/_depth` (late stamp). Agent-read: `goal/step/this.cs:121 List { internal set } = null!` stamped by `Admit` (late stamp); `call/list/this.cs:109` `Current` getter/setter read different slots; `call/this.cs:111,311` setters that do other work; six copies of the frame `Create` switch (`call/{binding,composed,action,goal,isolated,step}`); `call/action` `Module`/`Name` flat copies of `Action`.

### B5 — minor, agent-read — dropped answers
`call/action/this.cs:79` `WriteText(...)` result ignored; `snapshot/this.Resume.cs:47` `error.Callback(context)` ignored; `test/this.cs:144` ignores the Data `Code.Register<IAnswer>` now returns (it can refuse).

### B6 — minor, agent-read — event list
`event/on/list/this.cs:36-44` `this[item]` scans an array by reference on every fire, twice per level (the base used a keyed dictionary); `on.unbind` never removes the item's entry, so the scan grows with per-request items. Bench was in band, so not blocking. `event.Read` is a thrown `void` while its caller answers (two error routes). `event/this.cs:78` hand-parses "before"/"after".

### B7 — minor, agent-read — registry/code
`code/this.Load.cs:44-54` a refused later interface leaves earlier ones registered and the DLL unrecorded (a snapshot then misses live providers); `code/this.Snapshot.cs:9-60` `dll/provider/default` getters read live state, setters write `_layer` (stored twice/split lifecycle); `code/this.Snapshot.cs:98-116` `is not {} x → return kind` can answer Ok early on a decline without Error; `code/this.Snapshot.cs:104` case-sensitive compare; `code/kind/this.cs:26` plain `ArgumentException`.

### B8 — minor, plausible/unreproduced — sign vs verify view
`signing/code/Ed25519.cs:36-39` settles in `action.View ?? Out` and hashes in `action.View ?? data.Type?.View ?? Out`; `signature/this.cs:110 Hashed` re-hashes in `Value.Type?.View ?? (Stored ? Store : Out)`. A value whose type has its own view signed with an explicit view hashes differently on verify. Not reproduced; the snapshot path is green because its view is the type's.

### B9 — minor, agent-read — read seam
`format/item/reader.cs:92` reads an unread `source` through `source.Read` (widened to `internal`), skipping `Value`'s `%ref%` resolve, template render, the snapshot `Load` and the materialize catch. `list/serializer/Reader.cs:32` string indexer throws `KeyNotFoundException` for an unknown kind. `Depth = 64` (writer) vs `Deepest = 128` (parser) — decision 76 said one limit — and the parser throws plain `InvalidOperationException`.

### Nits / notes
N1 `app.Start` removal above. N2 `CLAUDE.md:65` still says a run's bindings are "held on the context of the actor (`actor.on`)": the branch moved them to `app.event` — a docs proposal. N3 `error/this.cs:62` `new string Key` hides item's new `Key(context)` door, `dict.Key` a third meaning. N4 wire record carries `properties` and `property` a letter apart — Ingi named `property` (decision 66), so ruled; pin it with that decision. N5 `type/item/property/list` `IsSequence => true` for a name-keyed bag. N6 `shortcut/this.cs:20` string-prefix on a path. N7 codeanalyzer verdict.json / tester verdict.json are stale FAILs — write the PASS. N8 `event.on.declared` class and `Declared`/`_names`/`Names` (ruled to event-action).

## Ruled / not counted
Pre-load walk in `View.Load` (decisions 94/103); `Kept<T>` (P7-2, with Ingi); the double firing on resume (328/329, with Ingi — its delta would come to me); `on.Write` via `writer.Write(@event)`; the Low writable members (security 307).

## Next-branch list
B4–B9, N-items; `member` writability Lows from security 307.
