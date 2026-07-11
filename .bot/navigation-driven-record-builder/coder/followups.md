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

## `list.@this.FromRaw` / `dict.@this.FromRaw` dissolution — deferred (the risky half)
Architect ruled these die too (`stage3-courier-name-answer.md` §141). `data.FromRaw` is dissolved
(4 prod callers inline `new Data(name, type.Create(raw, ctx, format))`; test callers use a
`Make.FromRaw` helper — SC3, keeps the lazy-source behavior suite). But `list`/`dict.FromRaw` are
**not** just a rename: `list.FromRaw` recursively converts **nested raw lists** to native eagerly,
where the perimeter's list ctor is type-on-read (nested lifts happen lazily at `.Value()`). The 6
`module/list` actions (flatten/remove/unique/sort/set/reverse) rely on "FromRaw already converted
nested raw lists" — rerouting to `Create` shifts eager→lazy and could break `flatten`'s nested
detection. Needs verification (run the module/list + CompareRedesign suites against the reroute)
before landing; deferred so it doesn't muddy the open binary cluster.

## Flaky: path/facet scheme-init race (pre-existing)
7 tests (`PathConversion_ResolvesScheme`, `PathParameter_*`, `FileReadStep_StringPathParameter`,
`Is_Facet_ImageIsPath`) flip red/green between identical Types runs — a scheme-registration race on
parallel first-use, not a real regression. Confirmed by re-run (47 then 40, the 7 the exact delta).
Worth a dedicated fix (eager scheme init / lock) so name-diffs stop flickering.

Also relevant to the open byte-backed-source ClrType question
([[collapse-byte-backed-source-clrtype]]): the unknown-mime fallback `type.Create("binary", …)` is a
concrete site where a byte[] borns declared `binary` (now a lazy source).
