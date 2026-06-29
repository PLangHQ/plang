# Codeanalyzer review of architect plan v2 — for architect + coder

**Branch:** `value-construction-redesign`
**Author:** codeanalyzer · 2026-06-29 · reviews `../../architect/v1/plan.md` (v2, commit `791de42a1`) and the coder's `../../coder/v1/plan-review.md`
**Verdict:** **PASS with one required fix** — the plan is sound and ready to sequence once the constructor narrative restores the dropped **typed-absence** case (§1). Every other claim verified against live code on `origin/value-construction-redesign`.
**Next bot:** coder (the fix is a narrative/sequencing correction the coder will carry into the stage files; no production code to write yet).

---

## What I verified

I checked every load-bearing claim in both the architect plan and the coder's review against live source. All hold:

| Claim | Source verified | Status |
|---|---|---|
| Container JSON-string break (the crux) | `dict/serializer/Reader.cs:20` calls `reader.BeginObject()`; `dict/this.Convert.cs:22` *is* the eager `text→dict` door being deleted; `item/serializer/json.cs:131` `return value;` returns a raw string unparsed | ✅ real — format-by-type is the right fix |
| `Reader.cs` (ITypeReader), not `Default.cs` | construction enters via `Readers.Reader()` → `Typed()` → `_runtimeTyped`/`_generatedTyped` (`type/reader/this.cs`), the `ITypeReader` pull — `Default.cs` is the `Of()`/whole-payload contract | ✅ correct |
| Wider reachable set + missing-reader leak | `file`/`directory`/`url`/`permission` ship only `Default.cs`, no `Reader.cs`; `Readers.Reader` throws `NotSupportedException` (`type/reader/this.cs:122`); `source.Value`'s catch is `JsonException or FormatException or InvalidOperationException` only (`source.cs:98`) — `NotSupportedException` escapes to the courier | ✅ strongest catch — confirmed |
| `choice` keyed by enum name, not `"choice"` | `type/this.cs:288-296`; no `serializer/` folder; `Convert` in `choice/this.cs` | ✅ correct — own sub-task |
| `Parse` contract (my v1 addition, now folded) | `json.cs:131` returns a plain string untouched; natives-out only `JsonElement`/`JsonNode` | ✅ invariant now stated explicitly |
| `NotSupportedException` catch-gap as a decision (my v1 addition, now folded) | Stage-1 exit now says "decide whether `source.Value` should catch the missing-reader throw as defense" | ✅ folded |

The merge-order flip (this branch lands first, no rebase gate; reconciliation is read-path-unification's problem at *that* merge) is a clean decision — the deletion conflict surface (`source.cs` context-less fallback, `Build`/`Judge`/`type.Convert(object?,ctx)`) is known and explicitly assigned. No objection.

---

## §1 — Required fix: the three-way fork dropped the typed-absence case

Collapsing the constructor narrative into a clean **three-way fork** (already-native / raw-scalar→`text/plain` / raw-container→`application/plang`) silently dropped a fourth case that v1 had as its own bullet ("no value + declared type → typed absence — unchanged").

The live ctor has it as a **distinct arm at the top of the exact block being rewritten** (`data/this.cs:193-201`):

```csharp
if (value == null && _item is global::app.type.@null.@this)
    // A declared type with no value yet — a typed absence (a tool
    // parameter slot, a typed null). The declaration must survive
    // even with nothing to lift.
    _item = new global::app.type.@null.@this(type.Name, type.Kind);
else if (_context != null)  … type.Build(_item)
else                        … type.Judge(_item)
```

`json.Parse(null)` returns null, which matches **none** of the three new cases — not "already a native dict/list/number", not "still-raw string", not "byte[]". A coder implementing the three-way fork literally would leave `set %x% as number` (no value) and typed-null tool-parameter slots with no arm, dropping the typed declaration down to a bare null citizen.

The leaf-trace row still says "rewrite the `Build`/`Judge`/context fork" but does **not** flag that the `value==null` typed-absence guard must be **preserved** through that rewrite — and the headline fork no longer mentions it at all.

This is almost certainly an editing omission (v1 had it; the three-case framing ate it), not a design change. But it is the one place the plan-as-written would mislead a literal implementer.

**Fix:** state the fork is **four cases** — typed-absence first, then the three value-bearing cases:

> The constructor, when a non-polymorphic type is declared, forks **four** ways:
> 1. **no value + declared type** → typed absence `null.@this(type.Name, type.Kind)` — the declaration survives with nothing to lift (tool-param slots, typed nulls). **unchanged.**
> 2. **already-native** (Parse returned a dict/list/number/…) → hold as-is.
> 3. **still-raw string/`byte[]` + scalar type** → `source(value, type, text/plain)`.
> 4. **still-raw string + container type** → `source(value, type, application/plang)`.

---

## §2 — Two non-blocking notes for the coder to carry into staging

1. **`byte[]` + container type is unspecified.** The fork pairs `byte[]` with the scalar case ("still-raw string/`byte[]` + scalar type"); the predicate line punts byte-format to "`FromRaw`'s existing format logic". A `byte[]` declared as a container (`as dict`) has no explicit arm. Likely a non-case in practice, but Stage 1's reachable-set enumeration should confirm it's unreachable or assign it a format — don't let it fall through silently.

2. **`as binary` with a kind has special narrowing** (`type/reader/this.cs:111-118`): binary narrows to the kind's inner type when that type owns a reader, else rides as binary. Stage 1's reachable-`(type, kind)` trace should account for this branch — the flat per-type gap table won't surface it.

---

## Net

The core design is right and unchanged from v1: kill the eager `Build`/`Judge` door, mint a `source` declared as the type, materialize once through the `ITypeReader`. The coder's four corrections are correctly folded; my two v1 additions (Parse contract, catch-gap decision) are folded. The only thing standing between this plan and a faithful implementation is the **typed-absence arm** — restore it to the fork narrative (four cases, not three) and the plan is ready to sequence.

**PASS — next bot: coder.**
