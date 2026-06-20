# `text.translate` — kill `tstring`, fold translation onto `text`

> Status: design seed, one open question (sender's vs reader's locale). Translator itself is Phase 6+ — nothing to build yet. This note exists so the thread survives a context clear.

## The realization (Ingi)

We have a `tstring` type. We should not. The `text` type should carry a boolean `translate` property — when true, the value goes through the translator. It has **nothing to do with templating** (`%var%` resolution). It is just `text.translate = true`.

(Initial framing was `translatable(currency|velocity|…)`. Dropped — see "Why a boolean" below.)

## Facts on the ground (verified 2026-06-20)

- `TString` lives at `PLang/app/data/TString.cs` — a `[PlangType]` wrapper: `Value`, optional `Key` ("translation key, future use"), and a `_resolver` for `%var%` interpolation. Doc comment: "Translation lookup is deferred to Phase 6+."
- **`TString` is vestigial.** No production code constructs or consumes it as a type — the only references are doc comments (`type/catalog/this.cs:510`, `data/JsonString.cs:11`, `module/builder/validateResponse.cs:192`). There are tests (`PLang.Tests/Data/App/DataTests/TStringTests.cs`) and a rename todo (`Documentation/v0.2/todos.md` "2026-06-10 — Rename TString").
- **`text` already owns templating.** `PLang/app/type/text/this.cs` has the full `%var%` machinery: `Template` (from `item.@this:196`), `HasHoles`, `Authored()`, `IsRef()`, and `Value(asking)` which full-match-hops to the live variable or single-pass interpolates. So `TString`'s interpolation job is a duplicate of `text` — OBP smell #3 (same logical thing stored twice).
- **`text` is a bare string on the wire.** `Write(IWriter w) => w.String(_value)`, `Shape => "string"`. `Kind` is *not* serialized — it's re-derived from the file extension at build. So `text` today has **no mechanism to carry a per-value flag on the wire.**
- The builder still *teaches* the LLM to emit `type:"tstring"` for templated output (`os/system/modules/output/write.notes.md` "tstring vs string"), and `action-catalog.md:23` documents `tstring` = translatable string. Those teachings are the leftover surface that the change has to retire.
- **No translator exists.** `grep translat` in production hits only `TString`'s doc comments + unrelated "exception translation" / "translate mime". Purely future work.

## The design

Kill `TString`. Add a boolean to `text` (Ingi's word: `translate`; C# property `Translate`, `init`-only, default false — stamped at construction like `Kind`/`Template`, immutable, no restamp in place). When set, the value is routed through the translator at the output edge.

Templating stays untouched and orthogonal. Retire the `tstring` teaching: the builder emits `type:"text"` (or `string`) and marks `translate` instead of emitting a distinct type name.

### Why a boolean, not `currency|velocity`

Currency/velocity aren't text. A velocity is number + unit; localizing it means converting mph→km/h **from the number**, not re-parsing a formatted string. By the time it's `text` the quantity is gone. Those belong on their own typed quantities that own their locale rendering — never as values of a flag on `text`. `text.translate` answers exactly one question: is this string human prose or not.

### Why the flag belongs on the value, not the action

The same `output.write` emits both `"Welcome back, %name%"` (translate) and `"DEBUG: %x%"` / routing tokens (don't). If the action translated everything, you lose per-literal opt-out. Putting `translate` on the `text` value gives the builder per-string control — the whole point.

## Two interactions that have to be wired right

**1. Order vs templating.** Orthogonal in concept, ordered in the pipeline. Translate the *authored* template first, then fill holes: `"You have %count% messages"` → `"Þú átt %count% skilaboð"` → fill `%count%`. Translation must read `Authored()`, not the rendered form, or you translate a string with a number already jammed in.

**2. Where the flag rides — the real decision.** `text` is a bare string on the wire; a bare string can't carry a per-value boolean. Two shapes:

- **(A) .pr-only.** `translate` is authored metadata persisted in the .pr. The `text` value is born with `Translate=true` at load; translation fires at `output.write` locally with the actor's locale; what crosses any runtime channel is already-localized bare string. The bare-string wire invariant survives untouched.
- **(B) richer wire shape.** `text` serializes as `{value, translate:true}` whenever the flag is set. Simpler to reason about, but gives up "text is always a bare string."

I (architect) lean hard on **(A)**: translation is an output-edge act that needs the reader's locale, so it fires at the channel, and the localized result is what travels. The flag only has to persist in the .pr.

## OPEN QUESTION (this is the fork)

**Does `translate` need to survive a runtime channel hop, or does it die at the first output edge that localizes?**

- If a `text` with `translate=true` can be passed actor→actor and translated by the **receiving** actor's locale → the flag must ride the runtime wire and (A) collapses into (B).
- If translation always happens at the **emitting** actor's output edge → the flag is .pr-only, the runtime wire stays bare, (A) holds.

Reframed: is locale the **sender's** or the **reader's**? That single answer picks (A) vs (B) and shapes everything downstream.

## When this proceeds — first contact / movie

Developer writes `- write out "Welcome %name%"` → builder LLM marks it `translate` (replacing the `tstring` teaching) → .pr carries the flag → runtime builds a `text` with `Translate=true` → `output.write` reads it, on the localizing edge calls translator(authored text, locale) → fills `%name%` → writes localized string to the channel. Demolition along the way: delete `TString.cs`, retire `tstring` from `write.notes.md` + `action-catalog.md`, delete/repoint `TStringTests.cs`, close the 2026-06-10 rename todo (superseded — there is no type to rename, it's a property now).
