# For architect — number kinds CAN'T be per-App/context-born (evidence); they must be context-free

**From:** coder. **2026-07-10.** Building the number roll-out per `stage2-number-answer.md`. Hit a
hard contradiction with the answer's premise while wiring the three switches to the kinds. Surfacing
before I either rework the foundation or improvise a deviation.

## The premise that fails

The answer states number kinds are **per-App, born with context** — which is why "levels carry the
kind by NAME" (a static Ladder can't hold per-App instances) and why they ride the shared
`App.Type.Kind[name|clrType]` door (per-App collection).

**But the code proves number can't reach a per-App collection.** Two of the three switches run
**context-less**, and `number.@this` carries **no `Context`** at all:

```
• number.@this          — has NO Context property (grep: none). Numbers are born ctx-less
                           (arithmetic mints them with no App in scope).
• serializer WRITE       — number.Write(IWriter w) → Default.Write(this, w). No context param,
                           no way to thread one (it's the value writing itself).
• FromDoubleAsKind       — called from arithmetic (DoAbs/Floor/Ceiling/Round in this.Unary.cs),
                           ctx-less by the answer's own "arithmetic runs in ctx-less operators."
```

`App.Type.Kind[...]` is a **per-App** collection (born with context, Mints with context). Write and
FromDoubleAsKind have no context and no App — they **cannot resolve a per-App kind instance**. Only
CoerceToKind and serializer-Read (both hold a ctx) could.

## Conclusion + proposal

**Number kinds must be CONTEXT-FREE** — resolvable from ctx-less arithmetic and the value's own Write.
Concretely:

- A **context-free number-kind registry** — static singletons, `Kind[name]` / `Kind[clrType]`
  (discover-once, no context). Each kind is stateless behavior (Build/Write/Read/FromDouble are pure);
  there is nothing per-App about "how int serializes."
- **Not in the global `App.Type.Kind`** (that one is per-App and holds *value-navigation* kinds
  json/list/dict, reached only where a context exists). "Same as json/list/dict" holds for the
  SELECTION SHAPE (ClrForm + name/clr door), not the collection or its lifecycle. Number kinds are a
  number-internal concern.
- The **Ladder** then keys by name (or holds the kinds directly — both work once kinds are ctx-free);
  the answer's name-indirection is still fine, just no longer forced by a per-App constraint that
  doesn't exist.

## Impact on what I've built

The 15 kind classes exist and compile (int committed as proof-of-shape; the other 14 built, held
uncommitted pending this). Their **behavior is identical either way** — only the base + ctor change:
today `: app.type.kind.@this` with `(context)`; context-free means a small `number.kind.@this` base
with a no-arg/parameterless registry. ~15 one-line ctor edits + a tiny registry, then the wiring
(CoerceToKind→Build, serializer→Write/Read, FromDoubleAsKind→FromDouble, all ctx-free) is clean.

**Question:** confirm context-free standalone number-kind registry (my proposal), and I proceed. If
you still want them in the per-App door, I need to know how Write/FromDoubleAsKind get a context —
because as the code stands, they can't.
