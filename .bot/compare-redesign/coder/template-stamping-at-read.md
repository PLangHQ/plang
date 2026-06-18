# Design: template-stamping happens at read, gated by the reader's mode

**Status:** designed + agreed with Ingi (this session). Supersedes `template-stamping-at-build.md` (build-time + per-node wire marks — rejected, see §9).
**Owner:** coder. **Branch:** compare-redesign.
**Related:** `list-dict-raw-slot-model.md` (the O(1) raw-slot model — landed; this is its FOLLOW-UP #1, now resolved).

> **You own the code and test shape.** Everything below — signatures, the reader-flag name, where exactly the mode is set, the test list — is the *intended design and the reasoning behind it*, not a spec to transcribe. If a cleaner shape falls out while you build, take it; just keep the two invariants in §3 (stamp on read, trust on the reader) and the security test in §8.

## 1. Why

A builder-authored value with `%var%` holes (`output.write content="Hi %name%"`) must render those holes at use-time. A value a user typed at runtime (`"%secret%"`) must print literally — never resolve. The flag that separates them is `item.@this.Template` (`"plang"` = authored; `null` = literal).

Today that flag is applied **after** the value is built, by a post-parse walk: `goal.list.Add` / `FromWire` / `GoalCall` each call `Action.StampTemplates()`, which calls `Data.Authored()` → `StampedForm(instance)` — a `switch(text/list/dict/source/clr)` in `data/this.cs` that rebuilds the value stamped, recursing into containers.

Three things are wrong with that, and one is now actively broken:

1. **Misplaced behavior (OBP).** `Data` is a courier. A `switch` on the value's concrete type that reaches inside each case to stamp it is behavior that belongs on the value, not on `Data`.
2. **Retrofit, not birth.** Authored-ness is re-derived by walking the value on every load, after it was already fully built once.
3. **Fragility (live bug).** The walk assumes container reads return stable instances. The raw-slot model made a container read born a *fresh* item per call, so "stamp the entries, then rebuild from the same entries" re-reads the container and discards the stamps. It's patched with a materialize-once hack in `StampedForm` — that hack is interim debt this design removes.

## 2. The model

**A value is stamped the moment it's read, inside the parser, gated by the reader's mode. There is no second pass over the action list.**

- The reader instance carries a `Template` mode (a string, e.g. `"plang"`), set at its construction site — exactly the way `Wire` already carries `Sign` and `View`.
- The **goal/`.pr`-load reader** is constructed in `Template="plang"` mode. While parsing, it stamps every `%ref%` leaf (and the container above it) as it borns them — one pass, on read.
- Every **runtime-ingest reader** (`http` body, `… as json`, CLI args) is constructed with `Template` unset. A `%ref%` string borns bare → `Template == null` → prints literally.

The trust boundary *relocates*: today it is "which code path called `Add`" (the seam); now it is "which reader read the bytes." Same guarantee, moved from a post-parse call onto a reader-construction flag. The seam's `StampTemplates()` calls go away.

**The `.pr` format does not change.** A `%ref%` already rides as a bare string today (`value: "Hi %name%"`, container leaves bare). It stays bare. The change is entirely in *how it is read*. Consequences: **no `.pr` migration, no rebuild** of existing artifacts, and **no signing question** — `Template` is never written to the wire, so it is trivially outside every canonical/signed shape.

## 3. The two invariants (keep these, whatever the code shape)

1. **Stamp on read, in one pass.** The stamp is set while the parser borns the value (`TextLeaf` already runs the `%ref%` regex to decide raw-vs-text — it now also sets `Template`). No traversal of the value *after* it is built.
2. **Trust lives on the reader instance, set at construction — never inferred from content.** A `%ref%` in the bytes is *not* the signal (runtime input contains those too). The signal is "this reader was constructed to read authored content." Set `Template="plang"` at exactly the goal-load construction sites and nowhere else.

## 4. Flows

**Authored, partial interpolation** — `set %x% = "Hi %name%"`

```
.pr on disk:  value: "Hi %name%"                       ← bare, unchanged

LOAD (goal reader, Template="plang"):
  json.Parse → TextLeaf sees %ref% → born text{ Template="plang" }   ← set ON READ
  action lands already stamped. No StampTemplates. No walk.

USE: output.write content=%x% → _type.Template != null → render → "Hi Ingi"
```

**Nested container** — `set dict %x% = message:%message%, user:%user%, mode:fast`

```
.pr on disk:  value: { "message":"%message%", "user":"%user%", "mode":"fast" }   ← bare

LOAD (Template="plang"): the parser builds the dict and, per slot:
   "%message%" matches %ref% → slot stores text{ Template="plang" }   (elevated item slot)
   "%user%"    matches %ref% → slot stores text{ Template="plang" }
   "fast"      no ref        → bare literal slot, untouched
   any stamped slot present  → the dict itself is born Template="plang"
```

The stamped leaf rides in its slot as a `text.@this{Template}` *item*, not a bare string — the same way a `signature` element rides intact in a slot (raw-slot model §"why store the item on write"). A raw string can't carry a `Template`; the item can. A read borns from the slot and returns the item as-is (the raw-slot read returns an already-typed slot unchanged), so the stamp survives the fresh-per-read model that broke the walk.

**Runtime input — the security flow** — `http.get` body / `… as json`

```
body:  {"x":"Hi %secret%"}        (or even a forged {"@schema":"data",...,"value":"%secret%"})
  → parsed by the http / as-json reader, Template UNSET
  → "%secret%" borns bare → Template == null → prints "Hi %secret%" literally   ✓ safe
```

`%secret%` never renders because the reader that read it was never in `Template` mode — not because of anything in the bytes.

## 5. Leaf trace — the incumbent and every call site

**Incumbent behavior:** template stamping is `Action.StampTemplates()` → per-parameter `Data.Authored()` → `Data.StampedForm()` (the type-switch), with `text.@this.Authored()` doing the text arm and `RawGraphHasRef`/`StampEntry` doing the container/clr arms.

**The three seams that call it (all are authored-content deserialization through a reader):**

| Seam | File | Disposition |
|---|---|---|
| `.pr` → registry | `goal/list/this.cs:48` (`action.StampTemplates()` in `Add`) | **Remove the call.** Covered by the goal reader being `Template="plang"` upstream. Keep the rest of `Add` (App wiring, indexing). |
| goal-call `.pr` load | `GoalCall.cs:315` and `:325` | **Remove both calls.** GoalCall parses the `.pr` through the goal reader (`result.Value()` → `Ready()`); that reader is `Template="plang"`. Keep the App / `Step.Goal` / Parent wiring. |
| wire-rebuild | `this.FromWire.cs:49` (`act.StampTemplates()`) | **Remove the call — but trace first (the one risk item).** `FromWire` rebuilds an action from values that may have been parsed *upstream*, not from raw bytes it reads itself. If those values came from a `Template="plang"` read, the stamp is already on them. If `FromWire` does its own byte-parse anywhere (recovery chains, compile-response rebuild), that parse must be in `Template="plang"` mode. Confirm which, or an authored action comes back unstamped. |

**Validation vs construction:** stamping is pure *construction* (born the value in its authored form). It has no validation seam — nothing rejects on a missing stamp; an unstamped value is simply literal. So there is no validation call site to split out here.

## 6. Demolition worklist

**Dies when the reader-mode parse lands (all in `PLang/app/data/this.cs` unless noted):**

- `Authored()` (~455) — the entry the seams called.
- `StampedForm(instance)` (~466) — the `switch(text/list/dict/source/clr)`. This is the OBP smell being removed.
- `RawGraphHasRef(v, depth)` (~533) — the clr-graph ref scan; folded into the parser.
- `StampEntry(entry)` (~553) — the container-entry restamp.
- `HasTemplateRef(value)` (~562) — verify no caller outside the above; if clean, delete (the parser has its own `RefRegex`).
- `Action.StampTemplates()` and the whole file `PLang/app/goal/steps/step/actions/action/this.Templates.cs` — nothing else lives in it.
- `text.@this.Authored()` (`text/this.cs:185`) — its only caller is `StampedForm`. Replace with the parser borning `text{Template="plang"}` directly, **or** keep a type-owned "born my templated form" method and have the parser call it (OBP-preferable — the type owns what its authored form is). Coder's call; do not leave it dangling with no caller.
- The 3 `StampTemplates()` call sites (§5).

**Stays — explicitly do NOT remove (the render side reads `Template`, it does not set it):**

- `item.@this.Template` (`item/this.cs:195`) — the stamp itself. Load-bearing.
- `Data.IsStampedTemplate` (~642), `HasVariableReference` (~150), `IsVariable` (~142) — render-time gates on `_type?.Template`.
- `Data.AsCanonical` render path (~722–758) and the use-time resolution — unchanged; they consume the stamp.
- `IsDataMarked` (~58) + the marked-`Data` reconstruction in `item/serializer/json.cs:42,121` — that path is for `signature` layers and genuinely-nested `Data`, **not** templates. Templates ride as bare `%ref%` strings, so this design never touches it (and that is precisely why there is no nested-deserialize plumbing problem — see §9).
- `RefRegex` (`item/serializer/json.cs:139`) — stays; now also drives the stamp, not just raw-vs-text.
- `text.HasHoles`/`IsRef`/`Kinded` — verify callers; `HasHoles` may lose its `RawGraphHasRef` caller but keep if `text.Authored`'s replacement uses it.

## 7. The new shape — where the changes land

- **`Wire` instance** (`data/Wire.cs`): add a `Template` mode property (string?, e.g. `"plang"`/null), mirroring `Sign`/`View`; thread it into the `json.Parse(...)` call at `Wire.cs:349`. Constructor gains the param (default null = off).
- **Parser** (`item/serializer/json.cs`): thread the mode through `Parse → ObjectLeaf/ArrayLeaf → RawSlot` and `TextLeaf`/`BornFromRaw` (all currently static, no mode param — pick a clean carrier: a `bool`/`string?` param, or fold it onto the existing `ReadContext`). When the mode is set and a string matches `RefRegex`, born `text{Template="plang"}` (top-level via `TextLeaf`; container slot via `RawSlot`) and mark the container `Template="plang"` when any slot is stamped.
- **Reader construction sites:** locate where the goal/`.pr` reader is built and set `Template="plang"`. Leads: `app.System.Channel.Serializers.Deserialize<goal.@this>(...)` (`goal/list/this.cs:392`), the goal source-door `Ready()` reached from `GoalCall`, and the `FromWire` reader (§5 risk item). The per-actor serializers (the `Out`/`Store` `JsonSerializerOptions` pair that already carry `Sign`/`View`) are the likely home. Leave `object.json.Read`, `… as json`, and `CommandLineParser` in default (off) mode.

## 8. Tests (suggestions — test-designer owns the final set)

- **THE security test:** the goal reader stamps a `%ref%` (renders at use); the http/as-json reader does **not** (prints literally). Same bytes, two readers, opposite outcomes. This is the invariant that matters most.
- Nested container: dict with `%ref%` leaves + one literal leaf → ref leaves render, literal untouched, container stamped.
- Full-match `%x%` (hops to live variable) vs partial `"hi %x%"` (renders) — both stamped at read.
- **Modifiers + Defaults coverage:** `StampTemplates` stamped `Parameters` *and* `Defaults` *and* recursed `Modifiers`. The reader must stamp values in all three positions — a templated default and a templated modifier param must still render. Easy to regress; make it explicit.
- `CommandLineParser` input with `%x%` stays literal.
- Existing `AsT_*` / `DeepResolution` tests that call `.Authored()` directly will not compile (method gone). Rebuild them to produce pre-stamped values (read through a `Template="plang"` reader, or construct `text{Template="plang"}` directly) and rename per `list-dict-raw-slot-model.md` FOLLOW-UP #2 (they exercise `Value<T>()`, not the dead `As<T>`).

## 9. Rejected alternatives (do not relitigate these without new information)

- **Build-time decision + per-node `template` marks persisted on the wire** (the superseded `template-stamping-at-build.md`). Rejected because: (a) it bloats the `.pr` — every templated leaf becomes a `@schema:data` envelope, undoing the raw-slot model for authored containers; (b) it forces every existing `.pr` to be rebuilt or lose rendering; (c) the per-node mark is *not self-enforcing security* — the same parser reads untrusted runtime JSON, so a forged `{@schema:data,…,template:"plang",value:"%apikey%"}` in an `http` body would render on the receiver. Putting trust on the *reader* (this design) closes that without signing the mark.
- **One `template` flag on the param + scan `%var%` leaves at load.** Rejected: the scan is a second loop to find the leaves — the exact post-parse pass we are removing. If the truth isn't on the reader, it has to be per-node, and then you're back to bloat.
- **Keep the seam, call a virtual `Authored()` after parse.** This was an intermediate idea (clean OBP, kills the switch) but it is still a *second traversal* after the value is built, and it keeps the trust on the seam rather than the reader. Folding the stamp into the parse (this design) is one pass and moves trust to the reader instance.

## 10. Verify while building

1. **`FromWire` input provenance** (§5) — the one place an authored action could come back unstamped.
2. **Render consumption post-raw-slot** — confirm `dict.Value()` / list render reads the stamp off the born item: a stamped `text.@this` slot must return intact on read (it should — raw-slot reads return already-typed slots unchanged), so the container's render loop sees `Template` on the leaf.
3. **Single trusted construction site** — the goal-reader `Template="plang"` should be set in as few, as obvious places as possible. That line is the security boundary; keep it greppable.
4. **Pre-existing regex ambiguity (separate todo, not this diff):** `%[^%]+%` mis-fires on legitimate percent text — `"50% off %item%"` matches `% off %`. Both the old walk and this design inherit it. Worth a `todo:`; out of scope here.
