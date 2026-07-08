# Coder review of the architect's plan

**Branch:** `navigation-driven-record-builder`. Reviewed `architect/plan.md` against source (traced every cited seam). **Direction is right and I'll build it:** target-owned navigation-pull, `Create`/`Convert` async, reflective-default-first with codegen deferred, and the Data-leaf seam left byte-identical. The three sequencing decisions (hand-write `action` first, async as a prep branch, keep `dict.Clr`'s STJ for non-records) all hold. Below are the issues I hit tracing it — one is a real cost the plan doesn't budget (I1), the rest are seams to nail before Stage 2.

---

## I1 — [must fix in plan] There are TWO async spines; Stage 0 names only one

The plan's async sweep (Stage 0) names the **`Value<T>()` → `Create`** spine: `data/this.cs:512` becomes `await T.Create(await Value(), this)`. Correct — but `list<T>.Convert` is **not** reached through that spine. It's reached through the **convert-hook** spine:

```
convert.@this.OfStatic(familyClass, value, kind, ctx)      // convert/this.cs:43
  → Invoke → hook.Invoke(...) as data.@this               // convert/this.cs:55  (reflective, SYNC)
```

Callers of that spine, both **sync**, both invoking the hook that is about to go async:

```
variable/list/this.cs:476   OfStatic(clrProp.PropertyType, value, null, _context)   // the write property-arm
  └─ inside SetValueOnObject — private object SetValueOnObject(...)  (SYNC, this.cs:364)
catalog/Conversion.cs:216   OfStatic(family, value, kind, null)
```

The moment `list<T>.Convert` returns `ValueTask<Data>`, `hook.Invoke(...) as data.@this` (convert/this.cs:55) returns a `ValueTask<Data>`, not a `Data`. So **`OfStatic`/`Invoke`, `SetValueOnObject`, and `Conversion.cs:216` all have to go async too** — and `SetValueOnObject` is the sync reflection helper at the heart of the deep-write. That's a ripple past "~40 signature changes, sync leaves return `new(result)`."

Not fatal — the callers are already under async action handlers — but Stage 0's scope ("pure signature sweep, no behavior change, the dispatch at `data/this.cs:512`") **undercounts the work**. Please add the convert-hook spine to Stage 0: `OfStatic` → async, `SetValueOnObject` → async, `Conversion.cs:216` await. Either that, or keep `list<T>.Convert` sync-shelled and only its record-element recursion async — but that reintroduces sync-over-async at the element pull, which is banned. I think OfStatic goes async; want your call before I size Stage 0.

## I2 — [Stage 2 design gap] the generic default needs a "record vs scalar" fork + a per-property leaf-switch

Decision 2 puts the mechanism in the **default** `ICreate.Create`. But the ~40 implementors include scalars (`number`, `text`, `date`) that do **not** navigate-pull — they convert one scalar. Navigation-pull only applies to **records**. So the generic default becomes:

```
pass-through (value is TSelf)        // KEEP — free, zero-alloc
facet chain                          // KEEP
error-value demotion                 // KEEP
if TSelf is a record  → navigate-pull each declared property   // NEW
else                  → convert-hook → dict/list Clr           // existing tail
```

Two things the plan doesn't pin:
- **How does the default know TSelf is a record?** (reflection: >1 init prop? a marker attribute?) This branch is the crux of Stage 2 and is unspecified.
- **Inside the navigate-pull, each property is itself a fork:** `List<Data>` property → Data reader (the seam); nested record → recurse `Create`; `list<T>` → `Convert`; else → `Value<declaredType>()`. That's a per-property **type-switch inside the default**. Is that acceptable OBP (it's dispatching on the *slot's* declared type, which the record legitimately owns), or does it want to be metadata the generator emits later? I lean "acceptable — the record owns its own property shapes," but flag it because a reviewer will read it as a switch-in-a-default.

Please confirm the record-detection primitive and that the per-property leaf-switch is the intended shape (not a smell to design out).

## I3 — [seam, both paths] how does a navigated clr(json) child physically feed the Data-leaf reader?

The plan says Data-typed properties (`action.Parameters : List<Data>`) are read "through `app/data/reader`… byte-identical." But that reader consumes a **`Utf8JsonReader`** off raw `.pr` bytes (`data/reader/this.cs` reads tokens, `RawValue()`, `Peek()`). Navigation hands `Parameters` as **clr(json) children** (`data.@this` over `JsonElement`), not a byte reader.

So the handoff is either: (a) re-serialize the child to UTF8 and re-run the reader (a round-trip — the smell we're removing), or (b) give the Data reader a navigation entry point (`Read(clr(json) child)`) that produces the same deferred/`%ref%`/template/signing Data the byte path does. **This is unspecified and it's load-bearing** — it decides whether `%ref%` params and signing stay identical.

Note this bites **both** paths, not just Stage 3 read:
- **Write path (Stage 1):** the LLM compile result emits params like `value="%productName%"` — a full-match `%ref%` that must born a `variable`, not a text. If Stage 1 builds `Parameters` from clr(json) children **without** going through the reader's `%ref%` branch, those refs die on the write path, not just the read path.

Which of (a)/(b) do you intend? If (b), that's a new method on `app/data/reader` and should be a named Stage-1 deliverable.

## I4 — [blocker-1 precision] the format-guess is token-driven; the proposed reroute is type-driven

`data/reader/this.cs:79-80` today:

```csharp
deferredFormat = reader.Peek() == TokenKind.String
    ? Text.Mime : "application/plang";     // token-driven
```

The plan wants "`object`/`dict`/`list`/json-kind-**declared** wire value → json → clr(json)" — that's **declared-type**-driven. Mixing the two axes is exactly where the variable-as-value seam can break:

- A **full-match `%ref%`** arrives as a **String token** → stays `Text.Mime` → resolved by its source in `type.Build` (`type/this.cs:265`). **Must stay untouched** — the plan flags this, good.
- The change should therefore only reroute the **non-String** branch (or the "declared type is a json-kind" case), never the String branch. Please state the rule as: *"String token → unchanged (text/plain, incl `%ref%`); non-String **or** json-kind-declared → clr(json)."* As written ("json-kind-declared → clr") it reads like it could catch a String, which would kill `%ref%`. Confirm the String branch is provably out of scope.

## I5 — [confirmed OK, minor] `list<T>.Convert` does receive the un-lowered carrier

Traced the worry that Convert only ever sees `Clr<object>()`-lowered raw (which couldn't `EnumerateItems`). It doesn't: the write property-arm passes the **carrier** — `OfStatic(clrProp.PropertyType, value, …)` where `value` is the `item.@this` (`variable/list/this.cs:476`). So `list<action>.Convert` can ask `value.EnumerateItems()` (clr delegates to its json kind, `clr/this.cs:111`). The only change is the guard at `this.Generic.cs:54` (`value is IEnumerable` → "is a navigable carrier"). Confirming so you don't need to re-verify — the mechanism closes.

## I6 — [logistics] base + rebase of this branch after Stage 0

This branch already carries commits (spec + plan). Stage 0 (async sweep) lands as its own branch and merges first. Off what base does Stage 0 branch — `variable-as-value`? And this branch then rebases onto the merged result? Just confirm the base so the mechanical diff doesn't collide with the spec/plan commits already here.

## I7 — [confirm no gap] signing of navigation-built params at save

Params built from clr(json) on the write path (Stage 1) skip the Data reader's sign path (I3). When the goal is saved (`goalsSave` → `Wire.Write`), does sign-if-missing cover them, so a navigation-built param signs identically to a byte-read one? I believe yes (sign fires inside `Wire.Write`), but it's the kind of thing that silently diverges — one line of confirmation, or a round-trip test in Stage 1's DoD.

---

## What I'm NOT worried about (traced, clean)

- **`dict.Clr` STJ stays for non-records** (Decision 3) — verified the non-record call sites (`variable/list/this.cs:440/456/477`, typed `Dictionary<string,T>`). Only the `ICreate.cs:61-62` record branch retires. Correct.
- **Stage 1 hand-write before generic default** — right call; de-risks the async doors on two real records.
- **Demolition worklist** — cross-checked file:line, all accurate.

## Ask

I1 (second async spine) is the one that changes Stage 0's scope — need your call on OfStatic-goes-async before I size it. I3 (reader handoff) is the one that decides whether `%ref%`/signing stay byte-identical — need (a) vs (b). I2/I4 I can proceed on with a one-line confirmation each. Rest are FYI.
