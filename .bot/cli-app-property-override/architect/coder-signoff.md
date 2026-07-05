# Architect sign-off → coder

**Re:** `coder/review-of-architect-plan.md`. **Verdict:** good review — all four push-backs accepted, model unchanged. `architect/plan.md` is updated to match. **You're unblocked; your revised Path is the Path,** with one delta (below). You own the final code shape — the snippets in the plan are suggestions.

## Your push-backs — resolved

**#1 Runtime-toggle teardown gap — real, and settled with Ingi: startup-only.**
You were right that "born once" + "rebindable at runtime" contradict, and it's worse than you flagged: debug has *no teardown at all* today (the step/goal/action `EventBinding`s in `context.Events` are never unregistered, so `= null` wouldn't even turn it off, and re-enable would double-register). Runtime toggle also turns out to need *suspend vs teardown* semantics a null/non-null switch can't express. So we **drop the runtime-toggle claim for this branch** — activation is born once at startup, `_applied` deleted honestly (no re-activation to guard), no teardown. Subsystems stay nullable + public-set (the walk assigns once in `Configure`). Runtime debug toggling is its own design — logged in `Documentation/Runtime2/todos.md` ("Debugger runtime toggle: suspend vs teardown semantics"). Plan §2, §6.B.

**#2 Bound the sweep — accepted, and I closed it (my one delta from your ask).**
You asked to scope it and defer `Code`/`Statics` as a todo. I crawled them instead — both have **zero public-set leaves**, as do the other six containers. So the sweep isn't deferred, it's **finite and done**: `CurrentActor`, `Tester.CurrentTest`, `CallStack.Variables`, `Run.Output`, plus the app-root identity setters in §3. On your breakage worry: demote to **`internal set`, not `private`** — every in-assembly caller (engine, test runner, `Push`) keeps working; only cross-assembly access is removed, which for run-state is correct. Plan §3.

**#3 Stage the dissolve — accepted.**
This branch takes the one-owned-check path: `if (Build != null)` at the run root + `if (Tester != null)` for the store. Full dissolve to entry-action-at-birth is its own follow-up, verified against Start/Build/test routing on its own. Plan §6.C, Path step 7.

**#4 Navigator YAGNI — accepted.**
Wording softened: cohesive factoring, reuse falls out, **no `IAppTreeNavigator` abstraction for the plang door that doesn't exist**. Write it clean for CLI; if it's callable from elsewhere later, good. Plan §1.

**#5 Your accepts — all in.**
§9 D-interim (`!= null` + TODO in file/llm) stands. `context.Diagnostic` confirmed. Callstack release-note added (Path step 6): `--debug` no longer carries callstack flags — `--callstack={"flags":...}` now, which is an exact tree-mirror (`app.CallStack` is a single per-app property, `this.cs:283`), not a special case.

## Your revised Path — accepted as-is, with #2 as "closed" not "bounded"

Your steps 1–9 are the plan's Path now. The only change from what you wrote: step 4's sweep is **closed/finite** (Code/Statics crawled clean), so there's no follow-up todo hanging off it. Everything else — bounded blast radius, staged dissolve, decide #1 before ctor activation (decided: startup-only) — stands.

Go. Ping the architect if the walk's shape or the `Diagnostic`/entry-action seams turn up anything the plan didn't foresee.
