# Crash #2 — `text.Value` StackOverflow: flow + fix direction (architect)

**Branch:** `fix-plang-build`. Responds to `coder/plang-build-findings.md` crash #2.
**Why:** the coder reverted the AsyncLocal cycle-guard (correctly — a band-aid) and asked the architect for the real fix. This is the flow, reproduced by running the code, and the natural-flow fix. No `isXxx` recall guard, no depth cap, no visited-set — those treat the symptom.

## Reproduced (not theorised)

Pure C# test, no builder involved — the recursion is independent of how the bad value got stored. Process aborted with a true `StackOverflowException` (exit 134 / SIGABRT). The runtime printed **the same two production frames repeated 6704 times**, everything else being async state-machine plumbing:

```
Stack overflow.
Repeated 6704 times:
--------------------------------
   at app.type.text.this.Value(app.data.this)   ← PLang/app/type/text/this.cs:72
   at app.data.this.Value()                      ← PLang/app/data/this.cs:260
--------------------------------
```

Repro (constructs the arming state directly, then reads it):

```csharp
await using var engine = global::PLang.Tests.TestApp.Create("/app");
var ctx = engine.User.Context;
var selfRef = new global::app.type.text.@this("%x%", "plang"); // stamped full-match naming itself
var data = new global::app.data.@this("x", selfRef, context: ctx);
await ctx.Variable.Set(data);
var got = await ctx.Variable.Get("x");
await got.Value();   // never returns — overflows
```

## The flow

Arming condition: variable `x` holds a **stamped full-match template whose own text is `%x%`** — a binding that is an alias to itself. Reading it runs a two-step loop forever:

**Frame A — `data.@this.Value()`** (`app/data/this.cs:260`)
```
266   var answer = await _item.Value(this);   // _item is the stamped text "%x%"
```
hands off to the item →

**Frame B — `text.@this.Value(data)`** (`app/type/text/this.cs:72`)
```
74    if (Template == null) return this;                  // Template="plang" → NOT taken
77    if (TryFullVarMatch(_value, out var varName)) {      // "%x%" → varName = "x"
79        var resolved = await context.Variable.Get(varName);   // returns the SAME Data bound to "x"
80        if (resolved == null || !resolved.IsInitialized)      // it IS initialized → NOT taken
87        return await resolved.Value();                   // ← re-enters Frame A on the same Data
```
→ back to Frame A on the identical Data. `Get → resolved.Value() → text.Value → Get` closes the ring. ~6700 cycles is just how many fit the default 1 MB stack.

Three guards on the path; with a self-alias binding none fire: line 74 (it's a live ref, not literal), line 80 (it's a genuine binding), line 87 (`Get` hands back the same instance, so the next call is identical).

## The fix is at the write/binding layer, not the read layer

Principle: **a variable binding holds a resolved value, never a live alias to a name.** Resolve the full-match `%ref%` at the moment it is bound, so the store can never contain a binding that points back at itself or around a cycle.

Why not fix `text.Value`: dereferencing a `%ref%` is the value door's correct job — `a = %b%, b = 5` must walk b to get 5. Any cycle-breaker there (identity check `ReferenceEquals(resolved, data)`, visited-set, depth cap) detects the symptom at the wrong layer, and an identity check is also incomplete — it catches `x→x` but not `a→b→a`. The read door should be free to deref and trust that bindings terminate. That guarantee has to come from what is allowed into the store.

## The mechanism already exists — `AsCanonical`

`data.@this.AsCanonical` (`app/data/this.cs:684`) already does the right move for a full-match: it hops `%x%` → `Variable.Get("x")` and returns the **target's current Data instance** — it does not call `.Value()`, so it resolves the alias without rendering and without recursing. `variable.set`'s no-type path already runs every assignment through it (the existing `%msg%` self-reference fix in `module/variable/set.cs`).

Direction: **promote that hop from a `variable.set` special-case to the variable write door (`Variable.Set`)**, so every store collapses a full-match alias to the instance it names — including whatever path is currently storing the raw `%x%`. A non-ref value (action results, infra vars) is a no-op; only a live full-match ref collapses.

## Why this dissolves it by nature

With alias-collapse at the binding boundary, an `_item` is never a bare full-match `%ref%`, by induction: literals are concrete, and `set %a% = %b%` stores b's *current instance*, concrete by the same rule.

- `x = %x%` can't be stored: the hop resolves to x's current value, or — if x is unset — to absent (`AsCanonical` already returns `IsInitialized=false` there), never to the text `%x%`.
- `a = %b%, b = %a%` can't form a cycle either: each assignment collapses the alias to a value at write time, so no live `a→b` pointer remains to close the loop. Eager alias-collapse means there are no live binding-level aliases to cycle through.

The read door then terminates in one hop, with no guard added.

**Lazy-passthrough stays intact** — this is a name-hop, not a render. `AsCanonical` returns the target's Data with its `_item` shared (a lazy file/url/channel source rides along unread), and container-element writes (`set %trace.plan% = %plan%`) go through the separate dotted path that already holds values as-is. The alias layer collapses; reads are not forced.

## One open verification before coding

Pin the single write that stores the raw `%x%` today — the path that bypasses the canonicalization `variable.set` already does. That confirms the fix is "a store that forgot to canonicalize" (put the collapse in `Variable.Set` and it's closed) rather than a write that goes around `Variable.Set` entirely (then the collapse has to sit on that path too). This is the provenance question; it is now the only thing left to nail, and it is a verification step, not a redesign.
