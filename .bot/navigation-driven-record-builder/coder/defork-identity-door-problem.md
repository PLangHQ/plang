# Defork follow-up: the identity-from-runtime-`System.Type` door — and there is NO overflow

Follow-up to your recursion answer (`born-native-lift-recursion-answer.md`, ruling (1)).
Two things to correct/decide. I'm blocked on the second and want your ruling before I
touch more code.

## 1. Record correction: the StackOverflow is GONE — there is no overflow to find

The defork per ruling (1) is **done and builds clean**. The `_clr`-only indexer removed
the entity↔apex loop — I ran the targeted suites and the full suite: **no
`StackOverflowException` anywhere**. The "overflow in Runtime.log" I mentioned earlier
was my mistake — a `grep StackOverflow` matched *source text* printed inside an unrelated
stack trace (the `catch (Exception ex) when (ex is not (… or StackOverflowException))`
filter lines), not a real crash. So: recursion fixed, nothing is overflowing. Please
don't spend on chasing an overflow — it doesn't exist.

## 2. The real problem: ruling (2)'s `of(System.Type)` — Ingi rejects the new method

Your ruling (2): *"add the mirror overload `of(System.Type)` … only if a caller actually
needs it."* A caller does need it, so I added it. **Ingi has rejected the new `of`
overload.** So I need a different shape for the same need, and I'm stopping here.

### What broke, precisely

The by-name full-suite diff (baseline HEAD vs defork) shows the defork introduces exactly
**2** new reds (the 3rd the raw count flagged — `SlashName_Resolved_ByRootRelative` — is a
pre-existing red in a flaky cluster; it fails identically on baseline). Both are callers
that held a **runtime `System.Type` and asked the indexer for identity**, which `_clr`-only
now (correctly) declines:

- `DistributedOwnerOfTests.Path_ReachedByIdentity` — `Types[typeof(path.file.@this)]?.Name`
  → `""`. `path` declares its base **Assignable**, and `Registry.cs:112` skips Assignable
  decls from `_clr` (`if (!decl.Assignable) _clr.TryAdd(...)`). Path subclasses only ever
  lived in `_typeToName`. Your q3 proof covered primitives / item-subclasses / non-item
  hosts but not the **Assignable-declared conversion owner** — its subclasses were reached
  *only* through the deleted fallback.
- `NumberValueTests.PlangTypeAttribute_Number_IsRegistered` — `types[typeof(number)]?.Name`
  → `""`. `typeof(number)` is the item type itself; it was never in `_clr` (only
  `int/long/…` are), only in `_typeToName`.

Both are **test-only** identity callers — no production code breaks from them.

### The one production caller that needs the runtime-`Type` identity entity

`build/code/Default.cs:937` (this is why I added `of(System.Type)`):

```csharp
var underlying = …;                          // a DECLARED param CLR type, in hand as System.Type
if (context.App.Type.of(underlying) is { } entity)   // needs the ENTITY, not just the name
{
    var carrier = new app.data.@this("", new @null.@this(entity.Name), context);
    if (entity.Create(p.Peek(), carrier) is { Type.Kind: not null } built)   // uses entity.Create + entity.Name
        p.Declare(built.Type);
}
```

It holds a runtime `System.Type` and needs the **identity entity** (to read `.Name` and
call `.Create`). On HEAD it was `context.App.Type[underlying]` — riding the same
`_typeToName` fallback the defork deleted.

### The collapse I think you'll want (no new door) — but it's your call

Identity-from-a-runtime-`System.Type` already decomposes into **two existing doors**:

- **name**: `GetTypeName(System.Type)` / `Name(System.Type)` (`type/list/this.cs:384,455`)
  — reads `_typeToName`, already the identity-name door.
- **entity from name**: the string indexer `this[string]` (name → entity).

So `context.App.Type[GetTypeName(underlying)]` gives the identity entity with **zero new
API** — `of(System.Type)` is just those two composed. The test callers collapse the same
way (or, if you'd rather, the two test assertions move to `GetTypeName` since they only
read `.Name`).

**Questions for you:**

1. Kill `of(System.Type)` and express identity-from-runtime-`Type` as
   `Type[GetTypeName(clrType)]` (compose the two existing doors)? Or a different shape?
2. Do you also want the pre-existing **`of<T>()`** reconsidered, or is Ingi's objection
   only to the new runtime overload? (`of<T>` predates this branch; ruling (2) leaned on
   it as "the identity door.")
3. Confirm the 2 test callers should re-point to whatever door you pick (they assert
   identity via the conversion indexer — the old conflated behavior).

### Current tree state

Defork core is in the working tree, **uncommitted**, builds clean. The rejected
`of(System.Type)` overload is present (used by Default.cs:937). My 2 test edits are
**reverted** — the 2 reds stand, documenting the real problem. Nothing committed.
