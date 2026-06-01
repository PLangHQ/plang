# OBP Cleanup — running collection

A backlog of **OBP shape violations** found during other work, parked for a *dedicated* pass rather than fixed inline (so focused feature branches stay focused and green). Append as you find them; fix in their own branch.

Rule of thumb for whether something lands here vs. gets fixed now: if fixing it is a **wide, cross-cutting refactor orthogonal to the branch you're on** (many callers, touches a shared registry), park it here. If it's local to what you're already changing, just fix it.

Reference: `Documentation/v0.2/object_pattern_formal.md` (the formal pattern + 9 rules), `Documentation/v0.2/obp-smells.md` (the smell checklist + worked examples).

Each entry: **location · the smell · the OBP-clean target · status · found-in**.

**Tooling & sequencing.** `tools/ObpScan` (syntax-only Roslyn scanner — run it over a root, get the H1/H2/H3 worklist) is the scanner for *this* pass. The plan is: **clean up the backlog below first, scanner-assisted; then promote the detection into a build-time `DiagnosticAnalyzer`** (a `PLNGxxx` diagnostic). Order matters — gating CI before the cleanup would just fail on the known backlog; the analyzer goes in once the violations are gone, to stop *new* ones.

---

## 1. `app.type.list.@this` — the type registry has accreted a wide verb/`Get` surface

**Location:** `PLang/app/type/list/this.cs` (`app.type.list.@this`).

**Found-in:** `type-kind-strict` (2026-05-31), while reviewing `BuildTypeEntries` usage.

**Status:** open — do NOT do it in `type-kind-strict` (coder is actively editing these files; it's orthogonal to the type/kind model). Its own branch after type-kind-strict lands.

**The smell** (the element types `image`/`number`/`path`/`hash` are clean — this is concentrated in the registry):

- **Collection-proxy verbs** — methods that hand back a collection the registry should simply *be/expose* (OBP: "Collections are the API; expose the collection, don't proxy it"):
  - `BuildTypeEntries(modules)` — a **verb proxying a collection**, and the name "TypeEntries" **restates the class's own identity** (the type list already *is* the type entries). Half of the lazy machinery already exists (`_catalogByName` is a `Lazy<>`); `BuildTypeEntries` is the leaked public construction verb.
  - `KnownTypes()`, `ComplexSchemas()`, `BuilderNames()` — same shape (methods returning collections).
- **Name-resolution thicket** (smell #3 — same logical thing exposed several times):
  - name→CLR through **three** doors: `Get`, `Clr` (`=> Get`), `ResolveType`.
  - CLR→name through **five**: `GetTypeName`, `Name` (`=> GetTypeName`), `ResolveName`, `GetTypeNameStatic`, `GetPrimitiveName`.
- **Redundant `Get`-twins** — a noun and its `Get`-prefixed double side by side: `ValidValues`/`GetValidValues`, `BuilderNames`/`GetBuilderTypeNames`.

**The OBP-clean target** (sharpened by Ingi 2026-05-31 — the first cut below was itself subtly wrong, see note):

- **The list owns all entries + all info, and returns itself.** Reading the entry list gives you the entries — no assembly smuggled into the getter. The construction work (walking modules/schemas) is *not* done behind a property named for the raw collection.
- **Each derived view is its own named member that owns its shaping.** The catalog is `app.type.entry.list.catalog` — a `catalog` property on the list that knows what to give for "catalog"; the old `BuildTypeEntries(modules)` module-scoped projection becomes *another* named view, **not** a parameter. A property returns exactly what its name says and does only that work — `Entries` returns entries, `Catalog` returns the catalog.
  - *Note: the first draft here said "Entries => _catalog.Value (lazy)". That's wrong — it names the property for the raw collection while doing catalog-assembly work behind it (work over modules/schemas that aren't the list's own). Name must match work; the catalog is a distinct named view, not a flavor of Entries.*
- Rebuild-on-`code.load` is a lifecycle event, not a getter argument; the registry owns its module set through `App`.
- **One** CLR→name member and **one** name→CLR member. Collapse the five/three down; drop the aliases.
- **Kill the `Get`-twins** — keep the noun (`ValidValues`, `BuilderNames`), drop the `Get`-prefixed double.

**Guardrail meanwhile:** branches touching this file must not *grow* the surface — no new registry verb, no sixth name-resolution door. (Stage 8's prompt-scoping already moves *away* from the all-types `BuildTypeEntries(null)` walk — good direction.)

---

## 2. Module `type/` entity files break the `@this` convention

**Location:** `PLang/app/module/<m>/type/<name>.cs` — e.g. `identity/type/identity.cs`, and siblings `http/type/http.cs`, `settings/type/settings.cs`, `math/type/math.cs`, `output/type/output.cs`, `loop/type/loop.cs`, `module/type/module.cs`, `list/type/list.cs`.

**Found-in:** `type-kind-strict` (2026-05-31, Ingi).

**Status:** open — cleanup/todo, not for `type-kind-strict`.

**The smell:** the convention is "a folder's primary class is `@this` in `this.cs`." A `type/` folder that holds **one entity** (e.g. `identity/type/identity.cs` describing the identity object) should be `type/this.cs` (`app.module.identity.type.@this`). Some of the siblings are different — `http/type/http.cs` holds a *bag of enums* (`HttpMethod`, `StreamFormat`, …), not a single entity, so it isn't the same violation. Review each: single-entity `type/` folders → rename to `this.cs`; enum/param bags can stay (or move to a clearer name).

**Note:** `data-internals.md`'s IdentityData section currently points at `identity/type/identity.cs` (the real file as it stands). When renamed to `this.cs`, update that pointer.

---

## 3. `path.permission.verb` — nullable verbs vs the always-present variant rule

**Location:** `PLang/app/type/path/permission/verb/this.cs` (`Read? Read`, `Write? Write`, `Delete? Delete`, `Execute? Execute`; `WhenWritingNull` omits unset verbs from the wire).

**Found-in:** `type-kind-strict` (2026-05-31). Surfaced reconciling `obp-smells.md` variant-design rule #3 ("variants always-present, non-nullable; never nullable as granted/not-granted signaling") against the live code, which does exactly that.

**Status:** open — cleanup/todo, not for `type-kind-strict`. Security-sensitive and orthogonal to the branch. The A-vs-B call (below) is made when the pass runs; Ingi leans B. Don't change inline now.

**The catch (why it's not a one-line fix):** the nullable is doing real work — **verb-level revoke**. `Read(Recursive:false, Metadata:false)` still grants basic single-file read (`Covers` returns true for a minimal read request), so an always-present-with-booleans model **cannot express "no read at all"** — only `null`/absence can. Removing `?` alone breaks revocation.

**The two coherent shapes:**
- **(A) Nullable = set membership.** Present verb = granted (with options); absent = denied. Compact wire (denied verbs omitted). Complete *given the verb vocabulary*. The cost Ingi flagged: absence is implicit — you can't distinguish "denied" from "data-loss" on the wire. If this is right, **narrow `obp-smells.md` rule #3** to "single-value variants, not set-membership grants."
- **(B) Always-present + explicit per-verb grant flag.** Every verb serialized; a `Granted`/`Allowed` bool (or equivalent) says yes/no explicitly, and revocation is `Granted = false`. Verbose wire, fully self-describing — Ingi's "serialize the verb so we know the permission." If this is right, **change the code** and keep rule #3.

Ingi's lean ("code is wrong, we should serialize the verb") points at **(B)**. Confirm, and whether the cleaner flag lives per-verb or on the `verb.@this` container.

---

## 4. Registry family — collection-proxy verbs (ObpScan H1)

**Location:** the `*.list` registries and `module`. Surfaced by `tools/ObpScan` (H1).

**Found-in:** `type-kind-strict` (2026-05-31). Generalises #1 (type registry) to the whole family.

**Status:** open — cleanup/todo, not for `type-kind-strict`. Fix together with #1 (same shape, same pass).

**Clear "list-everything" proxies** (verb-named method handing back a raw collection — should be the collection / a named view that returns itself):
- `variable/list/this.cs` — `GetNames()`, `GetAll()`
- `event/list/this.cs` — `GetBindings()`
- `module/this.cs` — `GetActions()`, `GetChannelInventory()`, `GetDefaults()`
- `actor/context/this.cs` — `GetEventBindings()`
- `type/list/this.cs` — `GetBuilderTypeNames()`, `BuildTypeEntries()` (already in #1)

**Compute/derive — borderline:** `goal/steps/step/actions/this.cs::ComputeBranchChain()`, `module/MarkdownTeaching.cs::ScanOrphans()` — produce a derived result, not a stored collection; lower priority.

**Confirmed smell (resolved 2026-05-31 — call sites settle it; Ingi was right, "derived view" was too lenient):** `event/list/this.cs::GetMatchingBindings()` and `GetBindings()`. *Every* caller does `foreach (var b in events.GetMatchingBindings(...))` (actor/context.cs:378–463) — get a raw filtered `List<EventBinding>`, then loop to run the bindings. The filter+iterate+run is the collection's own job. OBP-clean: the event collection owns the run (the `Actions.RunAsync()` pattern — `events.RunMatching(type, ctx, goal/step/action)`), not a raw-list handoff the caller foreach-es. `module/Events.cs` `Before`/`After` (`List<GoalCall>` via `Stamp(GetBindings(...))`) is the same family.

---

## 5. Value conversion lives in a central target-type switch, not on the types

**Location:** `PLang/app/type/list/Conversion.cs` (`TryConvertTo`); twin in `PLang/app/channel/serializer/Text.cs` (`ConvertFromString` + `IsSimpleType`).

**Found-in:** `type-kind-strict` (2026-06-01). Ingi noticed `Convert.TryConvert`-shaped code during the ObpScan/analyzer work and flagged it as a likely OBP symptom. Confirmed by reading `Conversion.cs`. Swept for siblings by ranking every non-`.build` `.cs` under `PLang/` by density of `== typeof(` (target-type switches) and `value is <type>` (value-shape branches) — `grep -c` per file, sorted descending. That ranking surfaced the twin (`Text.cs`) and the adjacent sites in the recurrence list below; it's the reproducible way to find more of this shape.

**Status:** open — cleanup/todo, not for `type-kind-strict`. The per-type `Convert` migration is already underway on this branch (`app.type.convert.@this` + `text/this.Convert.cs`); draining the rest, plus the `Text.cs` dedupe, is its own pass. Don't widen `TryConvertTo` meanwhile.

**The smell:** `TryConvertTo` (`Conversion.cs:108`, ~400 lines) is one switch keyed by **target type** — `if (targetType == typeof(JsonNode))`, `typeof(TimeSpan)`, `targetType.IsEnum`, a ~50-line `typeof(GoalCall)` arm, the path-string and ctor-string arms. Every arm is "how to make a value of type X from a raw value" = X's own knowledge, centralized in a file that has to reference every type to switch on it. To add a type you edit the switch instead of the type shipping its own hook.

**The twin (worse — it's a divergent duplicate):** `Text.cs:91 ConvertFromString` reimplements the string→primitive arm (`int.TryParse`, `long.TryParse`, `DateTime.TryParse`, `Guid.TryParse`, `byte[]`). It has already drifted: `TryConvertTo` parses with `InvariantCulture` (`Conversion.cs:451`), `ConvertFromString` uses bare `TryParse` (CurrentCulture) — the same `"3.14"`/date string deserializes differently depending on which path you came through. A latent locale bug. And `Text.cs:82 IsSimpleType` is a hand copy of `type/list/this.cs:480 IsPrimitive`. Two instances of smell #3 (same logical thing implemented twice) layered on top of the conversion smell.

**The OBP-clean target:** the correct shape already exists right next door — `type.Convert(value, context)` (`type/this.cs:149`) routes to the family's own `static Convert(object?, kind, context)` hook via `app.type.convert.@this.Of`, and `text/this.Convert.cs` is the first type to own its construction (`this.cs:162` calls `TryConvertTo` the fallback "each family grows its own Convert over time"). End-state: every type owns `Convert` (read) and a render hook (write); `Conversion.cs` and `Text.cs` shrink to dispatchers like `type.convert.@this.Of` already is; `System.Convert.ChangeType` survives only as the genuine primitive leaf. Smallest first step (kills the locale bug now): route `Text.Deserialize<T>`/`ConvertFromString` through the one converter and `IsSimpleType` through `IsPrimitive`.

**Where the shape recurs (from the density sweep):**
- `channel/serializer/Text.cs` — **real, divergent duplicate** (above). Highest value, smallest fix.
- `module/condition/Operator.cs` — `left is string` / `is Enum` / `IsNumeric` coercion for compare. Same logic as the `IBooleanResolvable` rule (a value's boolean meaning belongs to the value) → coercion-for-compare arguably belongs on the type too. Adjacent, lower priority.
- `module/{builder,http,assert}/code/Default.cs` — per-module value-shape branches; check whether each code-behind re-derives the same value handling. Unverified.
- `data/this.Normalize.cs` + `this.Reconstruct.cs` — `typeof` arms on the wire path. Likely the legit per-type dispatcher (`[Out]`/`IWriter`, per CLAUDE.md "Data is not enveloped") rather than a switch — confirm dispatcher-not-switch before parking.
- `type/list/this.cs::GetTypeName` — CLR→PLang **name** ladder repeated 3× (L107 / 332 / 731). Legit as a name registry (naming keyed by type, not behavior), but the triplication is an internal DRY smell.
- Legit, NOT smells: `type/number/this.IConvertible.cs` (the `IConvertible` interface contract inherently switches), `variable/navigator/List.cs` (`typeof(IList<>)` capability detection — the navigator's own concern).

---

## Decided NOT to change (so they're not re-flagged)

- **`app/error/*Error` naming** (`ActionError`, `ServiceError`, `ValidationError`, …) — **keep the `*Error` suffix.** The OBP-pure single-word form (`error/Action` → `app.error.Action`) collides with pervasive names (`System.Action`, the `Goal` alias) and the types are used too widely (ServiceError 36 files, ValidationError 29, ActionError 18) for full-namespace to be clean. The suffix does real disambiguation work — names still describe what the thing IS. Not a smell. *(Separate: `GoalError` and `ProgramError` have 0 references — dead-type deletion candidates.)*
- **`BuildResponse`, `LlmMessage`** (ObpScan H3) — left as-is; Ingi will refactor these a different way later.
- **ObpScan H3 in general** — most public mutable collections flagged are DTOs / `.pr` param-bags / error-detail shapes, not owned-state smells. H3 is a candidate list, not a fix list.

## Dead-code deletion candidates (0 references)
- `variable/list/this.cs::GetChangedSince()` — 0 callers (the string projection it does, `Value?.ToString()` / `"(null)"`, would also be a presentation-in-registry smell if it were live).
- `app/error/GoalError`, `app/error/ProgramError` — 0 references.
Delete in the cleanup pass unless a near-term consumer is known.
