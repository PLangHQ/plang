# codeanalyzer v1 — runtime2-callstack — findings

Reviewed coder/v1 implementation of the architect's callstack plan: 7 commits,
~15 new/replaced files. C# tests pass (2580/2580). Findings below; no MAJOR.

Severity legend: **MAJOR** breaks behavior or contract; **MINOR** misleading or
fragile; **NIT** simplification/cosmetic.

---

## PLang/App/CallStack/Call/this.cs

### MINOR

1. **Line 122-127 — `Diffs.Add` is not thread-safe.** OnSet is fired from
   `Variables.Set` without a lock (Variables/this.cs:107,128). Two parallel
   Task.WhenAll branches can both mutate the *same* Variables (the AsyncLocal
   captures the Call, not the Variables) and race on `Diffs.Add`. List<T> isn't
   thread-safe. Either lock around the Add or use a ConcurrentBag/queue.
   The Children list already gets `lock (caller.Children)` treatment in
   CallStack.Push for the same reason — Diffs has the same exposure.

2. **Line 222-226 — Children removal locks `Caller.Children` but doesn't
   re-check the `History` flag of the *Caller's* Push-time flags.** Today
   `_stack.Flags` is read live; a developer flipping flags mid-run via
   Debug.Apply (which the field is designed to allow per CallStackFlags.Default
   doc) would cause asymmetric Push/Dispose: Push retained the child under
   History=true, Dispose runs with History=false and removes it (or vice
   versa, leaking). Either snapshot Flags into the Call at Push time, or
   document that Flags are read-once at construction.

### NIT

3. **Line 168-178 vs 186 — `SnapshotChain()` and `Chain` return the same data
   but `Chain` allocates a fresh List on every PLang dot-path access.** Comment
   says "cheap — Caller chain depth is typically small," but `foreach %!callStack.Current.Chain%`
   in a hot PLang loop allocates per iteration. If this is for renderer use,
   that's fine; for PLang loops, consider caching while the Call is alive.

4. **Line 193-202 — `Depth` walks Caller again** instead of computing it from
   `Caller?.Depth + 1 ?? 1`. Each access is O(N). Same Caller-walk also
   exists in `CallStack.Push:78-83` for cycle detection. Three Caller-walks
   per Push (Push depth check + Push ContainsGoal + Call.Depth on access).
   Cache depth as `int Depth { get; }` set in the ctor.

5. **Line 248 — `catch (Exception ex) when (ex is not (NullReferenceException
   or OutOfMemoryException or StackOverflowException))`** appears 4 times in
   the new code (here, App.this.cs:429, App.this.cs:535, others). Already a
   project pattern, but worth a static helper `Exceptions.IsCatchable(ex)` to
   keep the predicate consistent if it ever changes.

---

## PLang/App/CallStack/this.cs

### MINOR

6. **Line 75-96 — Both depth-overflow and indirect goal cycle throw the same
   `CallStackOverflowException(MaxDepth)` with the same message.** A goal cycle
   isn't a depth issue; the message "exceeded N frames" is misleading when the
   actual fault is "goal X recursed via goal Y." Either give the exception a
   `Reason` field, or use a distinct `GoalCycleException` for the second site.
   Today a developer debugging "why does my goal-cycle test throw" reads
   "exceeded 1000 frames" and chases a phantom depth issue.

7. **Line 78-83 — Manual depth-walk loop duplicates `Call.Depth`.** Once Call
   caches Depth (finding #4), this becomes `if (caller.Depth >= MaxDepth)`.

### NIT

8. **Line 98 — `Push` passes `caller` twice** as both the `caller` and
   `previousCurrent` ctor params. They're always equal here (caller IS
   `_current.Value`, which IS what the Call needs to restore on Dispose).
   The parameter is structurally redundant. Drop `previousCurrent` from the
   ctor and have the Call read its own Caller for the restore — two facts,
   one source.

---

## PLang/App/CallStack/SerializableCallStack.cs

### MINOR

9. **Whole file is dead.** `grep` shows zero references in `PLang/` (only
   global-using aliases in `PLang.Tests/GlobalUsings.cs:60-61`, themselves
   unreferenced). The architect's plan called for "render-agnostic data
   so flamegraph projection falls out for free" — that's not the same as
   exporting a fixed shape, and nothing currently consumes
   SerializableCallStack/SerializableCall. Deletion test: remove this file +
   the two GlobalUsings lines → no test fails. Either wire it into Debug.cs's
   trace export (architect intent) or delete it.

---

## PLang/App/CallStack/CallStackFlags.cs

### MINOR

10. **Line 10 doc lies — `Tags` flag is never checked in code.** Both
    `Call.Tag()` (Call/this.cs:135-139) and `tag.cs:30-43` write tags
    unconditionally. The flag's documented contract — "no-op writes when off"
    — isn't enforced. Either add `if (!_stack.Flags.Tags) return;` to
    `Call.Tag` (and the tag handler), or update the doc to "the Tags dict is
    always lazy-allocated; this flag is currently advisory." Today flipping
    `--debug={callstack:{tags:false}}` doesn't disable tag writes.

---

## PLang/App/Errors/this.cs

### MINOR

11. **Line 15 — `_current` is `static readonly AsyncLocal<IError?>` on an
    instance class.** CallStack/this.cs:22 *deliberately* moved to instance-
    level AsyncLocal "so tests can spin up multiple CallStacks in the same
    process without polluting each other's Current." Errors does the
    opposite: `All` is per-instance, `Error` reads a process-wide static.
    Two App instances in the same test process see each other's `Error`
    even though their `All` lists differ. Match CallStack: make `_current`
    instance-level. (Found by clone/copy family audit — same shape, two
    decisions.)

### NIT

12. **Line 42-55 — `Restorer` has a `_disposed` guard but no `_owner` check.**
    Caller calling `Dispose` twice is fine; caller disposing out of LIFO
    order silently corrupts the AsyncLocal scope (writes its captured
    `_previous` over whatever the *current* nested scope is). `using`
    statements enforce LIFO so this is fine in practice, but if the
    Restorer ever leaks past its `using`, the corruption is silent.
    Optional: assert `_current.Value` matches what we pushed before
    restoring.

---

## PLang/App/this.cs (App.Run)

### MINOR

13. **Line 401 — `stack.Push(...)` runs OUTSIDE the try/catch.** Push throws
    `CallStackOverflowException` for both depth overflow and indirect goal
    cycle (CallStack/this.cs:82,96). When that happens, the exception
    escapes App.Run as a CLR exception instead of being translated into a
    `Data.FromError(ServiceError)` — every other failure path here returns
    a Data result. Wrap the Push (or move it into the try) so cycle
    overflow flows through the same ServiceError pipeline. Without this,
    the indirect-cycle test will see a thrown exception instead of a Data
    error result, and Step.RunAsync's catch (which excludes OCE) catches
    it as a "real" infrastructure failure rather than user-recoverable.

14. **Line 408 — `context.Step.Context = context`** mutates a shared Step
    instance and is not restored in `finally`. Step is per-goal, not
    per-dispatch; a parallel branch dispatching the same Step (legal if
    the user `goal.call`s the same goal in Task.WhenAll) overwrites
    another branch's Context pointer. Either snapshot+restore in finally,
    or move Context off Step onto the Call.

### NIT

15. **Line 401 — `await using var call = stack.Push(...)` followed by
    `try { ... } finally { restore anchors }`.** The Call disposes after
    the finally runs, which is correct; the comment at 440-442 spells this
    out. Consider extracting the anchor save/restore into a small
    `using` (e.g. `using (context.PushAnchors(action))`) so the dispose
    order intent is structural rather than commented.

---

## PLang/App/modules/error/handle.cs

### NIT

16. **Lines 95-127 — `GoalFirst` and `RetryFirst` branches duplicate
    structure** (recovery-then-retry vs retry-then-recovery). Two near-
    identical 14-line blocks. A small helper `TryRecover(actions, context,
    error, erroredCall)` returning `(bool handled, Data result)` would let
    each branch read as a 4-line sequence. Not blocking — readability win.

17. **Line 88 — `var erroredCall = stack.Current;`** captured before
    `await next()`. Comment explains why. The capture is correct, but
    the variable is named for what it WILL be, not what it IS at capture
    time (it's "current call before next"). Rename `currentCall` →
    becomes `erroredCall` only after we know `!result.Success`. Tiny.

---

## PLang/App/Variables/this.cs

### NIT

18. **Lines 95-119 — `Set(name, Data dv, ...)` has three branches that all
    end with `_variables[name] = dv;` and one returns `dv`.** The
    `if (prev != null && !ReferenceEquals(prev, dv))` and
    `else if (prev == null)` branches return early; the implicit fall-
    through (prev == dv, the "same instance rebound to itself" case)
    skips OnSet *and* returns dv — that's the no-op rebind path. Consider
    early-returning at the top: `if (ReferenceEquals(prev, dv)) return dv;`
    Two-line clarification.

19. **Line 197 — `_ = _context?.App?.Debug?.Write(...)` swallows a Task.**
    `Write` returns `Task` (presumably). Discarding it means the diagnostic
    message may not flush before the next operation. If Debug.Write is
    fire-and-forget by design, fine; otherwise `await` it.

---

## PLang/App/modules/debug/tag.cs — CLEAN

Small, focused, single-purpose. No-op when CallStack.Current null is the
right default. (Aside from finding #10's flag check.)

---

## PLang/App/CallStack/Diff.cs — CLEAN

3-property record with a focused docstring. Nothing to remove.

---

## Pass 5 — Deletion Test

- `SerializableCallStack.cs` (whole file) — finding #9.
- `GlobalUsings.cs:60-61` (test aliases for the above).
- `Call.SnapshotChain()` OR `Call.Chain` — finding #3, but both have
  callers in the App.Run catch path and PLang dot-path resolution
  respectively. Keep both, but note the `Chain` overlap.
- The `previousCurrent` ctor param on Call (finding #8).

Nothing else can be deleted without a behavior regression.

---

## Verdict: NEEDS WORK

No MAJOR findings — the implementation matches the architect plan and tests
pass. But there are two behavior-shaping issues worth fixing before next
phase:

- **#10**: `Tags` flag is documented to gate writes but isn't checked.
- **#11**: `Errors._current` static contradicts the deliberate per-instance
  design CallStack adopted for the same reason.
- **#13**: `CallStack.Push` exception escapes App.Run's exception-to-Data
  contract.

Findings #1 (Diffs thread-safety) and #14 (Step.Context mutation) are
correctness risks in concurrent paths — worth fixing now while the
patterns are fresh.

Suggest sending back to **coder** for these five (#1, #10, #11, #13, #14)
plus any of the NITs they want to take. The other items (#3, #4, #6, #8,
#9, #16) are good cleanup but not blocking.
