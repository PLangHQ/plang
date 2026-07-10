# For architect — number-kind CLR leaks: the answer's `Create(object)`/`Create(double)` are wrong shape

**From:** coder. **2026-07-10.** Implementing `stage2-number-context-free-answer.md`. Ingi caught a
string of CLR leaks in the kind shape while I built it — several trace back to the answer itself
carrying the OLD CLR-centric signatures over instead of lifting to plang types. Writing it up so the
architect settles the corrected shape before the ~30-file build lands.

## What the answer specified (and why it leaks)

Answer #2: *"the construction verb is `Create` — `Create(object)` and `Create(double)` overloads on
the number-kind base."* Both are CLR-primitive interfaces on a plang type:

- **`Create(object) → object`** (as I first wrote it, following the answer): a kind takes and returns
  raw CLR `object`. A plang value type's construction door must be **plang in, plang out** —
  `Create(item) → number` — like the other 12 relocated types. `object` is a clr leak.
- **`Create(double)`**: *why is a kind aware of `double`?* (Ingi). `double` is a CLR primitive; a
  kind (int/long/float) has no business with a raw `double` on its interface. This came from porting
  `FromDoubleAsKind(double, NumberKind)` verbatim — the old code was already CLR-centric.
- I then reached for **`System.Convert.ChangeType(value.Clr<object>(), ClrForm)`** to coerce — Ingi:
  *"why do you need changetype?"* Right — `ChangeType` is a generic CLR converter; the value should
  lower **itself** through its own door: `value.Clr(ClrForm)`.
- I then reached for **`number.FromObject(...)`** to wrap — Ingi: *"FromObject is dead code."* It
  folds into `Create`; no object-dispatch should survive.

The root: the old number construction (`CoerceToKind(object, NumberKind)`, `FromDoubleAsKind(double,
NumberKind)`, `FromObject(object)`) was CLR-primitive throughout, and the answer relocated the
*mechanism* faithfully without lifting the *interface* to plang types. Every one of these is the
"clr leak" / "opened box" smell from `obp-smells.md`.

## The corrected shape (coder + Ingi, for architect to bless)

```
number.kind.@this   (standalone, context-free — stateless behavior; the number VALUE carries its kind)
    Name ; ClrForm
    Create(item value) → number.@this          // plang in, plang out — the ONE construction verb
        // per-kind, TYPED — the value lowers itself, the typed number ctor rewraps; NO object, NO double:
        //   int:        number.Create(value.Clr<int>())
        //   half:       number.Create((System.Half)value.Clr<double>())
        //   biginteger: number.Create(value.Clr<System.Numerics.BigInteger>())
    Write(number, IWriter) ; Read(ref reader)   // serializer boundary (fine)

number.@this
    Kind → the kind instance                     // private static clr→kind map, context-free
    Create(int) Create(long) … Create(BigInteger) Create(Half)   // the From fold, => new(v)
    Create(item value, data)                     // ICreate courier: parse→number, resolve declared kind, kind.Create
    // From / FromObject DELETED — no object-dispatch survives

FromDoubleAsKind → a PRIVATE arithmetic helper at the Math boundary, keyed by kind NAME — NEVER a kind verb
Ladder → name-keyed (the re-key, as already ruled)
delete: NumberKind, CoerceToKind, KindFromName
```

**Key corrections vs the answer:**
1. Kind verb is `Create(item) → number`, not `Create(object) → object`. Plang types at the interface;
   CLR only at the real .NET boundaries inside (`value.Clr<int>()` = the number's own `ToInt32`; the
   typed `number.Create(int)` = the number wrapping a CLR primitive it legitimately owns; `IWriter`).
2. **No `Create(double)` on a kind.** The double-narrowing (a computed float result → my precision)
   is arithmetic's, at the `Math` boundary — a private helper keyed by kind name, same category as the
   integer Ladder's `Narrow`. It is not construction, and a kind must not know `double`.
3. No `ChangeType`, no `FromObject` — the value lowers itself (`value.Clr(...)`), the typed
   `number.Create(T)` rewraps.

## The open question for the architect

The only thing I want blessed before building: **is per-kind TYPED `Create` (no `object`-dispatch)
right, or do you still want a `number.Create(object)` boundary constructor?** My/Ingi's lean is
typed-only (no `object` anywhere) — it keeps the whole surface plang-typed and kills `FromObject`
outright. If typed-only, each of the 15 kinds carries a one-line `Create` (its `value.Clr<T>()` →
`number.Create(T)`); if you want a shared `object` boundary, the base gets one default and `FromObject`
becomes `number.Create(object)`.

Nothing past the (now plang-typed) base is committed; the 15 classes exist but need this shape locked
before I finish them + the fold + wire + re-key + deletions.
