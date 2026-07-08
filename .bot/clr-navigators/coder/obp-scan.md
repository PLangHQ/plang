# OBP scan — clr + kind machinery (`0687f97b7..c1b670afc`)

Scope: the branch's clr/kind work — kind behaviors, the token, clr delegation, the `type.Kind`
flip, clr(json) serialization, reader pivot, producer door. (The ~40 `.Kind?.Name` flip edits are
mechanical reads, not new shape.)

## 🟢 Alarms clean (forks first)
- **No behavioral forks.** `clr.Mint`'s kind derivation (`kind ?? Kinds[type] ?? ResolveName ?? FullName ?? "*"`) is a fallback *chain resolving one value* (the kind), not two execution paths for one operation.
- **No generic/fallback beside per-type handlers.** The `*` (reflection) kind is a *registered kind*, uniform with json — not a switch-in-a-dispatcher. `Kinds[k] → behavior ?? reflection` is a lookup-with-default.
- **No type-switch behavior in the registry.** Behavior lives on each `kind.behavior`; the registry only selects.
- **Verb+Noun clean.** `Navigate/Enumerate/Load/Convert/Output` (single verbs), `Of/Scan/Register` (idiomatic), `Scalar/Data/Step` (nouns / match the spec). `EffKind`, `KindOf`, `KindFor`, `EnsureInitialized` all deliberately avoided.
- **`.Clr` boundary.** `JsonElement`/reflection use is at the json/`*` kind boundary (the CLR bridge) — no new off-boundary lowering.

## 🟠 Borderline (note — mostly new surface beyond the architect doc)
1. **`kind.Output` / `behavior.Output` — a 5th capability.** The doc lists four (navigate/enumerate/load/build). I added serialization so a `clr(json)` renders raw json (kills the `valueKind` BCL leak) and the `*` kind owns the relocated reflection. Fits "the kind owns behavior," but it *is* surface not in the spec.
2. **`kind.Of(string?)` — new factory.** Not in the doc. Forced by the null-impedance: a reference-type implicit conversion can't null-lift, so `{ Kind = someString? }` would invoke the operator on null and throw. `Of` is the explicit nullable door (idiomatic — sibling of `Conversions.Of`/`reader.Of`). A landmine-free necessity, but new.
3. **base `Load` defaults to `text`** (rather than throwing) so md/unknown "load as text" per the doc, instead of every non-json kind declaring a loader. A behavioral default — flagged to Ingi.
4. **`ResolveName` retained in `clr.Mint` derivation.** Ingi flagged `ResolveName` as OBP-suspect; kept here because the identity tests require it (a `callstack` clr reports kind `"callstack"`). Confined to the clr's birth kind-derivation (the CLR bridge), not a loose call elsewhere.
5. **`.Kind?.Name` recurs ~40×** — resembles smell #5 (consumers apply the same transform). It's the *deliberate* explicit read (no `kind→string` implicit, by the landmine-free decision); reads the kind's identity name, not a wrong-shape workaround. Accepted, but the recurrence is real — if a cleaner owner emerges, collapse it.

## 🟢 Checked and clean
- **`dict.Convert`** reads `source.Value()` + matches `clr/JsonElement` — but it's a *leaf converter* (its job is to build a dict FROM the json source), so reading its own input is legitimate, not courier decompose (#7/#8).
- **`Data.Convert(kind) → kind.Convert(this)`** passes the whole `Data`; no decompose (#8).
- **Registry `list.@this`** owns its private dicts + `Register`/`Scan` surface (no external rule-enforcement, #1); born-correct static discovery (no `EnsureInitialized` flag).
- **clr** stays a pure carrier delegating navigate/enumerate/output to its kind; no reflection/`is JsonElement` switch left on it.

**Verdict:** alarms clean; the borderline items are all *new, domain-justified surface* (Output, Of) or *necessary retentions* (ResolveName for identity) — each noted for Ingi's awareness rather than a shape to fix.
