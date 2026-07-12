# Coder follow-ups (deferred, not blocking the collapse)

## `channel.StampType` — verb+noun + late-stamp (flagged 2026-07-11, Ingi)
`PLang/app/channel/this.cs:312` `StampType(context)` decides the channel content's plang type
from its MIME (`Format.TypeFromMime(Mime) ?? type.Create("binary", …)`), then does
`t.Context = context` before returning.

Two smells:
- **verb+noun** name — it's really "the channel's declared type for its content"; a noun property
  (`ContentType`) reads truer than a `Stamp…` verb.
- **late stamp** — `t.Context = context` right after construction; context should be handed at birth.

Surfaced while dissolving `FromRaw` (the channel boundary calls `StampType(context).Create(raw, …)`).
Pre-existing, not introduced by the collapse. Deferred by Ingi's call — clean up in a later pass.

## `list.@this.FromRaw` / `dict.@this.FromRaw` dissolution — DONE (commit 82973cace)
Resolved. The "eager recursion" fear was unfounded: in all 6 `module/list` callers the input is
`.Value()` (already a materialized `item`), so `FromRaw` only ever hit its `is list.@this` cast +
null-guard — the raw-IEnumerable recursion was **dead code** there (only test fixtures passed raw
CLR collections). Callers now pattern-match `if …Value() is not list.@this nl → error`; OpenAi's
raw-dict routes through `type.Create`; test fixtures use `Make.List`/`Make.Dict` (SC3). Both
`this.Convert.cs` files deleted, grep-zero in production, zero regression. `FromRaw` was "a cast
wearing a coercion costume" (Ingi).

## Flaky: path/facet scheme-init race (pre-existing)
7 tests (`PathConversion_ResolvesScheme`, `PathParameter_*`, `FileReadStep_StringPathParameter`,
`Is_Facet_ImageIsPath`) flip red/green between identical Types runs — a scheme-registration race on
parallel first-use, not a real regression. Confirmed by re-run (47 then 40, the 7 the exact delta).
Worth a dedicated fix (eager scheme init / lock) so name-diffs stop flickering.

Also relevant to the open byte-backed-source ClrType question
([[collapse-byte-backed-source-clrtype]]): the unknown-mime fallback `type.Create("binary", …)` is a
concrete site where a byte[] borns declared `binary` (now a lazy source).

## `!info` — a standardized metadata surface across all types (Ingi, 2026-07-12)
Deferred discussion. Today `%config!file%` (facet-value nav via item._prior) returns the origin
file/path VALUE — navigable FileInfo-like metadata (`!file.size`, `!file.exists`, `!file.fileName`,
`!file.mimeType`, path.file.Scheme/Extension/…). Ingi: `!file` is the wrong name; it should be a
STANDARDIZED `!info` property, registered by the type on creation, uniform across all types —
`!info` on image → EXIF, on text → length, on file → FileInfo. "Standardizing it would give us great
power." To design after the `type.list` (type-history) change lands. The file type should register the
info property on the object at creation (not reflection).

## `item.list` is internal — public nav exposure needs reflecting surfaces to skip it
The item's type-history collection (`app.type.item.type.list.@this`, at `type/item/type/list/this.cs`)
is exposed as `item.list` — but INTERNAL, not public. A public `list` property regressed 3 Modules
tests (LLM image query, Fluid file-path render, choice-from-text): a reflecting surface (LLM schema /
Fluid property enumeration) pulled `list` in and choked on the `type.list.@this` return type. Internal
fixes it (tests reach it via InternalsVisibleTo). To expose `%x.list%`/`%x.type.list%` navigation
later, make it public AND teach the reflecting surface(s) to skip it (an [LlmIgnore]/[Out]-style gate),
verified against those 3 tests.

## Kill NamespaceTail fully — ~19 domain types still free-ride it (Ingi flagged)
dict/list now declare their names explicitly (`Type => new("dict"/"list", typeof(@this))`).
NamespaceTail (a value computing its OWN type name via C# namespace reflection — the obpv) is still
the base `item.@this.Type` default, used by ~19 types that lack an override: actor, snapshot, test,
test.timing, variable, choice<T>, directory, permission, error, mock, code, LlmMessage, sign,
identity, settings, BuildResponse, path.StatInfo, type.list.view, and the `type.@this` entity itself
(verified via a make-abstract build — the CS0534 list). Fully killing it = making base Type abstract
and giving each an explicit `Type => new("<name>")` (name = its folder tail today). ~19 careful
overrides; deferred so it's not rushed. NameOf (ICreate error messages) also uses NamespaceTail — a
static naming helper, separate from the value's identity door.
