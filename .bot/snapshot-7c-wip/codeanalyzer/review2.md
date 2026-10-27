# codeanalyzer — snapshot-7c-wip re-check at 23d61537b (against my 36 items, d119e797f → HEAD, 82 files)

Requested by the architect (123, 151). Three readers read every file changed since d119e797f whole; I re-read item 1 and the View door myself (V). Verdict: **FAIL — item 1 is still open and is not on Ingi's list; a handful of small new and older smells remain.** Coder next.

## Status of the 36

| # | status |
|---|---|
| 1 signature `text? View` beside `Origin` | **OPEN (V)** `type/item/signature/this.cs:110–117` unchanged (`text? View`, a three-view list, `?? Origin…`); `data/schema/signature.cs:99–100` and `Ed25519.cs:65` still decide the view three ways. Not on Ingi's list. Fix: the signature holds an `app.View` set at birth, read once from its name. |
| 2 view by identity | waiting on Ingi (126: first on his list) |
| 3 view-less reader door | FIXED (one `Typed(name, kind, view)`; sites ask `ctx.View.Reader`); residue N1 |
| 4 `Synthetic` | FIXED (gone, six setters, tests); stale comment `action/serializer/Reader.cs:46–47` "non-synthetic" |
| 5, 6, 7, 11, 12, 14, 22 | Ingi's (verified present, not re-raised) |
| 8 clr drops view | FIXED (`clr/this.cs:117–120`, `reflection/this.cs:143`); the `kind.Read` signature is a locked change held for Ingi |
| 9 sensitive twice | FIXED (view writes null; list no longer skips) |
| 10 resume | FIXED, action half: `action.list.Start(context, from)` is the one door; `step/this.Resume.cs` deleted. My `else` trace was wrong (124); the real bug is fixed. Step half (`step/list` two loops, `ReferenceEquals`) = Ingi's Place |
| 13 result travels | FIXED (`Data.Take`, private; every copier carries Handled, Snapshot, Signature) |
| 15 `error.Goal` | FIXED (`Step?.Goal ?? _goal`) |
| 16 `step.List` | FIXED (`Admit`); stale doc `step/this.cs:118–120` |
| 17 late stamps | FIXED (memory born in context, `snapshot.Context` get-only); goal stamp in goal.list's weak table ruled (137) |
| 18 provider origin | Ingi (ICode) |
| 19 snapshot key readers | ruled (133); the 13 inline `new DeclinedException(new error(…))` not re-raised |
| 20 naked pair | FIXED (private `walk`) |
| 21 two Diff births | FIXED (one capture rule; `item.Copy()`; `EventsSince` once per frame); residue N4 |
| 23 literal type names | Match regexes FIXED (13 static); `Type.Name ==` forks = Ingi's; **one `new Regex` per dict entry still at `goal/step/action/formal/reader.cs:571`** |
| 24 debug binds | FIXED (`Bind`) |
| 25, 27 | Ingi's |
| 26 thunks ×5 | FIXED (private `door<TFunc>`) |
| 28 key normal form | FIXED (Ed25519 chain gone; strict); N3 |
| 29 cracked errors | FIXED in screen, path, http, test runner; `IncomparableException` Ingi's; **still cracking: `app/this.cs:418–437` `Unreadable(bytes.Error?.Message…)`** |
| 30 swallowed slot | backlog (ruled) |
| 31 middlemen | `app.Start(goal,ctx,ct)` FIXED; still open: `Data.Settle(View)` ↔ `View.Settle` (`data/this.cs:128`, `View.cs:110`), `wire/kind/plang/writer.cs:83–103` (17 forwards) |
| 32 `_pr` | FIXED (`Pr` derived; hops gone) |
| 33 `CallbackGoalNotFound` | FIXED (file deleted) |
| 34 `Parameter` | FIXED (`_arguments` apart from `_parameter`) |
| 35 comments | mostly FIXED; remaining below. My "128 unreachable" was wrong (140, mutation): accepted |
| 36 `Default.cs` | FIXED except Ingi's `GoalsSave`/`AppSave` and backlog `_buildTimer`; `Destination` below |

## Still to fix (coder)

1. **Item 1** (above): the one blocker.
2. **N1 [new beside old]** `View.cs:126` `Reader` = `Typed(this) ?? Reader.Reader(…)`, and `View.cs:138` `Make` repeats `Typed(this) ?? Typed(null)` inline with a different fallback. "Which reader does this type register in this view" has two homes; `Make` should ask one view member and `Reader` add only its record rung.
3. **N2** `call/binding/this.cs` `Rest` override equals `call/this.cs:473–474`; delete it (deletion test: nothing changes).
4. **N3** `Ed25519.cs:206–207` throws `FormatException` only to be caught at `:126` and turned into `SignatureInvalid`; `Verify` returns bool. The `Contains('=')` is a special case for one input shape.
5. **N4** `Diff.cs:15–50`: public `Diff(Name, Before, At)` beside the internal `(name, before, deep)` (two births); the second assigns `Before = Captured(…)` after the base call (a stamp); `Captured`, `IsScalar`, `Summary` use no state.
6. **`formal/reader.cs:571`**: the per-entry `new Regex`, the smell 23's fix left in this file.
7. **O5** `app/this.cs:418,428,434,437` `Unreadable(string? why)` fed `Error?.Message` / `ex.Message`: hold the Errors whole (`error.list`).
8. **O1** `type/item/variable/list/this.cs`: `GetChangedSince`, `GetNames`, `Save`, `Restore` have no caller in `PLang/` or `PlangConsole`; `Clone` and `ToDictionary` only tests. Remove with their tests (no runtime code for tests).
9. **Stale or orphaned comments:** `action/serializer/Reader.cs:46–47`; `step/this.cs:118–120`; `goal/this.cs:432,521`; `action/list/this.cs:26–28` and `:60–66`; `snapshot/serializer/Reader.cs:19` crefs to members that don't exist; `snapshot/this.cs:5–9` (line numbers, narrates a state); `actor/context/this.cs:155–157` orphan comment; `call/this.cs:24–29` summary over a `//` block; `variable/list/this.cs:57–60` ctor summary above `Outer`.
10. **Small:** `step/this.cs:154–156` `data.@this result;` then assigned (`var`); `data/this.Result.cs:37` `Error` pass-through over `_error`; `build/code/Default.cs:236` `Destination` returns `Error?` and inserts actions (a noun for a mutation); `module/http` `Head` returns a tuple and callers do `head!` (low); `item/this.cs:380` `_type ?? rebuild` (older, comment reworded, shape unchanged); `type/this.cs` `CreatableFamily` beside `Implementer` (low); `FormatAttribute.Store` bool beside `kind.View` (low: an attribute argument can't hold a View); the frame kinds' `Create(raw, type, data)` ×6 (O2).
11. `View.Load` walk re-serialises each nested object (`GetRawText` → `GetBytes`, `json.Typed` re-parse): a simplification, not a bug.

## To Ingi (verified present, not re-raised)
2, 5, 6 (`Clone`/`Copy`), 7, 8's `kind.Read`, 11, 12, 14, 18, 22, 23's forks, 25, 27, Place (step half of 10), `Uninitialized`, `IncomparableException`. One new proposal: `ctx.View.Reader(ctx.Context.App.type.list["x"], null, ctx.Context)` is written 6 times (`Properties.cs:74,75`, `data/reader:70`, `action/serializer/Reader.cs:130`, `error/serializer/Reader.cs:38`, `list/serializer/Reader.cs:32`): one door on `ReadContext` (the serializer is locked).

## Bench — the parts the diff touches (birth, request, ptest, sweep; no rest, no OpenAI), load 2.7

| Measure | base d72538402 | d119e797f (before) | HEAD 23d61537b | Band |
|---|---|---|---|---|
| Sub birth | 109 KB | 110 KB | **107 KB** | ±5 |
| Root birth | 2387 KB | 2493 KB | **2312 KB** | ±25 |
| Sub request | 542 KB | 578 KB | **529 KB** | ±10 |
| Root request | 3841 KB | 4076 KB | **3785 KB** | ±120 |
| Held MB (3,000 requests) | flat | flat | 0.1→1.9→1.9→1.9 (request), flat | no leak |

Times at load 2.7: sub birth 0.16 ms, root birth 19.0 ms, sub request 2.03 ms, root request 20.25 ms. The base row was taken at load 11, so its times don't compare. **All four byte figures are now under the base: the +106 / +36 / +235 KB are gone, so there is nothing to name or make lazy.** I did not run the by-type allocation count: with HEAD under base there is no extra to attribute. Say the word if you still want it.

`plang --test` 4.8 s, 298 pass / 2 fail (BareLiteralJudgedByForm baseline; FreshAppGoesOn, waiting on Ingi). Sweep 73.0 s, red: Wire (surface golden) and Runtime (readme), both the landing's; Data's two reds are green. SavesTheRun green.
