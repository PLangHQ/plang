# value-construction-redesign — findings + proposed plan (for architect review)

**Branch:** `value-construction-redesign` (off `read-path-unification` @ `3ddcdb17f`)
**Author:** coder · 2026-06-29
**Status:** investigation + proposed direction — NOT implemented. Architect to own the plan.

> Branched out of `read-path-unification` because this overgrew the Stage 6 *cleanup* stub. The
> read-path-unification branch's `stage-6.md` has been restored to its original cleanup scope; ALL of the
> convert-machinery / double-conversion redesign lives here instead. This is the deep dive Ingi asked for.

---

## TL;DR — the headline finding
Constructing a typed `Data` does a **double conversion**, and `type.Build` is a **redundant second
construction path** that diverges from the read path:

```
new Data("n", "5", type: number)
 data/this.cs:184   _item = Create(Parse("5"))        // NO type passed → lifts the raw to a throwaway intermediate
                      Create("5"): OwnerOf(string)=text → OfStatic(text,"5") → text.Convert("5") → text "5"
 data/this.cs:205   _item = type.Build(text "5")       // type = number
                      number.Convert(text "5") → number 5
                                                         //  net:  string →(text.Convert)→ text →(number.Convert)→ number
```

The intermediate `text` is an artifact of `Create` being called **without** the declared type. The READ
path does the same job **once, lazily**, with a `source`:

```
read:   source("5", {number})  →  .Value() parses once via the reader → number 5     (source.Raw = "5", clean)
```

So the real target is not "make `Build(item.@this)` route to a Convert table" (the direction stage-6.md
was drifting toward) — it's **converge the eager ctor onto the read's `source` mechanism so a value is
born ONCE as its declared type. Then `Build`/`Judge` dissolve.**

---

## What `source` is (it kept coming up; here it is precisely)
`PLang/app/type/item/source.cs` — a **born-with-bytes lazy carrier**: the undecoded raw form (`string`
or `byte[]`) + its declared `{type, kind}`, parsed only on first touch.
- `source.Raw` (`:47`) → the undecoded raw (the clean accessor).
- `source.Peek()` (`:62`) → the raw form (byte[]-declared-text decodes to its text face).
- `source.Value()` (`:88`) → parses via the reader registry → a NEW instance; the Data rebinds.
- It's the **READ path's** carrier (the wire/channel reader stamps it from mime, source.cs:7).
- **Note (already entangled):** `source.Read()` (`:120-129`) has a context-less fallback that ALREADY
  calls `type.Create(_type).Convert(s)` for a string raw — so `source` and `Convert` are already wired
  together. That fallback is flagged to die with the context-never-null work.

## The inspection — what flows into `Build`
**A `text`, not a `source`.** Trace `type.@this.Create(object? raw, context)` (`type/this.cs:372`):
- `raw is item.@this` → passthrough; `IEnumerable<Data>`/`List`/`Dictionary` → native list/dict;
- **`OwnerOf(raw.GetType())` → family → `OfStatic(family, raw)` (`:422-428`)** — a string is owned by
  `text`, so `Create("5")` calls `text.Convert("5")` → a `text.@this`. **This is the first conversion.**
- Then the ctor's `Build` does the second (`text → number`).

So the value at `Build` is a freshly-minted `text`, and `text` exposes its content only via `ToString()`
(its *display* edge, `text/this.cs:158`) or `.Clr` (a lowering) — no clean raw accessor like `source.Raw`.
That asymmetry (`source` has `.Raw`, `text` doesn't) is a real gap, but it's downstream of the actual
problem: **we shouldn't have made the `text` at all.**

---

## The convert-machinery design threads (settled in discussion with Ingi)
These hold regardless of the larger restructure, and should feed the plan:

1. **Ownership is target-owned, and stays.** A new `calendar` implements "make a calendar from text/…"
   inside `calendar` alone; nothing else changes. Source-owned (`text` knows how to become every target)
   would force editing `text` for each new target — an Open/Closed violation. **Decided: target-owned.**
2. **The hook is plang-typed in AND out.** `static data.@this Convert(object? value, …)` →
   `static item.@this Convert(item.@this value, …)`: no `object?`, no `.Clr<object>()` at the top,
   returns `item.@this` (not `data.@this` — kills `Build`'s `Convert(value).Peek()` unwrap), THROWS on
   failure (no `Data.Ok/Error` round trip).
3. **Dispatch keyed by the plang type, via a registered table — NOT `System.Type` + `MethodInfo.Invoke`.**
   `Dictionary<typeName, Factory>` where `delegate item.@this Factory(item.@this value, kind, context)`.
   Populate via **source-gen** (the generator already walks the type tree → literal zero reflection);
   interim = `Delegate.CreateDelegate` once on a cache miss (no per-call reflection). The CLR-boundary
   `convert.@this.OwnerOf(System.Type)` STAYS — it's the genuine .NET interop seam (`GetValue<int>`).
4. **"Instance, not static" does NOT fit target-owned.** A "make a T" factory has no `T` receiver
   (chicken-and-egg) — that's *why* it's static today. Instance-on-`item` would force source-owned. So the
   hook stays `static`; the fix for "no reflection" is registration, not making it an instance method.

The current CLR-centric machinery: `type.Build(object?)` `type/this.cs:232`; `type.Convert(object?)`
`:177`; the reflection hub `PLang/app/type/convert/this.cs` (`Discover` GetMethod + `Invoke`); the ~12
per-type `static data.@this Convert(object?, kind, ctx)` hooks (number/text/date/datetime/duration/guid/
bool/binary/choice/path/time + goal.call).

---

## Proposed direction (for the architect to challenge)
**Born once as the declared type — `Build` dissolves.** Pass the declared type into construction so the
value is made as that type directly, the way the read already does:

```
// instead of:  _item = Create(Parse(value));  if (typed) _item = Build(_item);   // two-step, throwaway text
// converge on: _item = Create(Parse(value), context, declaredType);              // one step
//   → when a type is declared, Create makes a `source(raw, type)` (lazy) OR routes the raw straight to the
//     target's Convert — ONE conversion, no intermediate text. source.Raw is then the clean accessor.
```

Net effect: `Build`, `Judge`, and the ctor's `if context Build else Judge` fork all dissolve into one
typed construction door; the Convert hooks become plang-typed + table-dispatched (threads above); the
context-never-null finish (`Wire._context`, `source.Context` → non-null) falls out.

---

## Open questions for the deep dive (architect)
1. **Inflow across shapes.** I verified `string → text` (the double-convert). Confirm `dict`,
   already-typed (`number`/`path`), and `null` inflows into `Build` — does every typed construction
   double-convert, or only scalars? (Containers return native from `Create` and skip the Build coercion.)
2. **Two materialization mechanisms.** A `source` can materialize via the **reader registry**
   (`source.Read` → `serializers[format].Read`) OR via the **Convert hook** (`source.Read`'s context-less
   fallback). Are these one mechanism or two? Unifying them is probably part of this.
3. **`source` vs a typed `source` from `Create`.** Does `Create(raw, type)` build a `source` declared as
   the type (lazy, reader-materialized), or call `Convert` eagerly? The first reuses the read path; the
   second is eager. Decide the laziness story.
4. **Registration mechanism.** Source-gen the `typeName → Factory` table (zero reflection, touches
   `PLang.Generators`) vs `Delegate.CreateDelegate`-once (reflect once). 
5. **Return-`item` error semantics.** Convert throws on failure → who catches (the ctor? a typed
   `ConvertException`?) vs today's `Data.Error` round-trip. `source.Value` already owns a
   binding-named `MaterializeFailed` story (`source.cs:88-114`) — converge on that?
6. **`text` raw accessor.** If any path still hands `Build`/`Convert` a `text`, leaves need a uniform
   raw-content accessor (so `text` matches `source.Raw`); or prove only `source` ever reaches it.
7. **Relationship to the read-path-unification Stage 6 cleanup.** That branch's Stage 6 stub still owns
   "retire value-ctor + delete Build/Judge, scope TBD." This redesign IS that retirement, done properly.
   Decide: does this branch supersede that Stage 6, or feed back into it?

## OBP constraints to hold throughout
Target-owned construction · plang types end-to-end (no `object?`/`System.Type`/`.Clr` off the .NET
boundary) · no reflection in the hot path · born-with-context (no construct-then-stamp) · one
construction door (no second eager path beside the lazy read).

## Pointers
`data/this.cs:176-213` (the ctor + Build/Judge fork) · `type/this.cs:232` (Build) · `:177` (Convert) ·
`:372` (Create) · `PLang/app/type/convert/this.cs` (the reflection hub) · `type/number/this.Convert.cs`
(a hook) · `PLang/app/type/item/source.cs` (the lazy carrier).
