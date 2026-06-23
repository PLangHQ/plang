# Output redesign — the item writes itself to the wire (branch `variable-as-value`)

**Date:** 2026-06-23. Agreed with Ingi. Successor to the Navigate redesign — same
principle (the item owns it), now for serialization.

## The disease

Serialization is a TWO-walk, sync pile:
1. `Text.cs:28` / channel pre-resolves the WHOLE value: `await data.Value()` (async) —
   walk #1, materializes everything up front.
2. `data.Normalize(View)` (sync) flattens domain objects into a tree (`NormalizeValue`
   procedural type-switch in `data/`), then `json.Writer.Value` / `Wire.cs` (STJ sync
   `JsonConverter.Write`) walks the tree — walk #2.

Two wrongs: (a) double walk (resolve-all, then serialize-all); (b) the flatten logic
(`NormalizeValue`/`NormalizeObject`) lives in `data/`, a type-switch, not on the items.
`variable` is a leaf passthrough → writes the raw `"%msg%"`, never its value.

## The model — `item.Output(IWriter, View)`

ONE async pass: each item WRITES ITSELF to the wire, resolving lazily as it reaches each
node. Merges `Normalize` (flatten) + `Write` (render) into one. No pre-resolve walk, no
intermediate Normalize tree. The `await`s happen in OUR walk between `Utf8JsonWriter`'s
SYNC buffer writes; one `FlushAsync` to the stream at the end (confirmed: Utf8JsonWriter
has no per-value async; async is stream-flush only — that's fine, we await in the walker).

```csharp
// data.@this  (replaces Normalize)
public ValueTask Output(IWriter writer, View mode = View.Out) => _type.Output(writer, mode);

// item.@this  (replaces Write + Normalize)
public abstract ValueTask Output(IWriter writer, View mode);
//   leaf (text/number/datetime/bool/guid/duration/date/time/binary/null)
//        → writer.String(...) / writer.Long(...) / ...           (sync write, no await)
//   dict → writer.BeginObject; foreach e: writer.Name(e.Name); await e.Output(w,mode); EndObject
//   list → writer.BeginArray(n); foreach x: await x.Output(w,mode); EndArray
//   variable → await Value(); resolved.Output(w,mode)   ← resolved HERE, lazily (self-ref guarded)
//   clr  → BeginObject; reflect host [Out] (Tagged.PropertiesFor); each value:
//            item child → await child.Output; IDictionary/IEnumerable → object/array;
//            primitive → leaf; foreign object → new clr(it).Output; else → throw OutputException.
//          (absorbs today's NormalizeObject + the raw IDictionary/IEnumerable/primitive arms)
```

## Invariants

- **No non-plang types flow through `Output`.** Every value is an item (leaf/dict/list/
  variable/domain) or a `clr` carrier (foreign objects — wrapped at Lift: `type.cs:395`,
  `data.cs:476`). `clr` is the ONLY CLR boundary. A raw type reaching `item.Output` is a
  Lift bug → `OutputException` (no silent fallback, no `OutputValue` static helper).
- **`data.Output → _type.Output`** — Data delegates, never `data.Instance.Output`.
- **Reflection lives in `clr`** (same as Navigate). A domain item we own either defines its
  own `Output` or is reflected via `clr` — TBD per type during the build.
- **View filter** ([Out]/[Store]/[Debug], [Sensitive]/[Masked]) applies in `clr.Output`'s
  reflection (was `Tagged.PropertiesFor` in NormalizeObject) — symmetric to today.

## Scope / order (build + test per step)

- [ ] `IWriter` already has the surface (BeginObject/Name/EndObject, BeginArray/EndArray,
      Null/Bool/Int/Long/Float/Double/String/DateTime/DateTimeOffset/TimeSpan/Guid/Enum/
      Decimal/Bytes). Confirm it needs no additions.
- [ ] `item.@this.Output(IWriter, View)` abstract; `data.@this.Output` → `_type.Output`.
- [ ] leaf overrides (text/number/datetime/date/time/duration/bool/guid/binary/null/image)
      — mostly the body of their current `Write`.
- [ ] dict/list overrides (object/array walk, await children).
- [ ] variable override (resolve + delegate; self-ref guard — `_resolveDepth` in Value
      stays as the loud backstop).
- [ ] clr override (reflect host [Out] + raw arms; the CLR boundary; throw on non-plang).
- [ ] channel serialize entry: drive `data.Output(writer)`, `await FlushAsync()`. Replace
      the STJ `JsonConverter.Write` (`Wire.cs`) + `json.Writer.Value` walk + `Text.cs:28`
      pre-resolve.
- [ ] delete `Normalize`/`NormalizeValue`/`NormalizeObject`; `NormalizeException` →
      `OutputException`. Update ~10 callers.
- [ ] full `./dev.sh full` — serialization changed for every type; suite is the gate.

## Context

The Wire READER (`Wire.cs ReadBody`, born-on-wire) is the read half — UNCHANGED here;
this is the write half. Current blocker that motivated it: `variable.Cacheable=false`
(so a goal-call `planStep=%item%` re-resolves per call instead of memoizing the shared
descriptor) exposed that serializing `%msg%` (EmitBuildEvent) resolves a variable; the
lazy single-pass Output is the clean home for that resolution.
