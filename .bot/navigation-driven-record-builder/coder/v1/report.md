# Coder report — Stage-2 TryConvert deletion close-out

Branch: `navigation-driven-record-builder`. Commits `2a519aa25` (A+B) and `78e4671a3` (architect Q), pushed.

## Done (committed, green)
**A — the CLR Convert door + TryConvert deleted.** `PLang/app/type/list/Conversion.cs` gone (−614). Zero production callers: values self-convert (`item.Clr`), types self-build (`Create`), the reflection kind reads records from dict slots. `GoalCall`'s JsonElement arm → `channel.serializer.json.Options.Read()` (the one shared read-options owner).

**Unresolved-`%var%` hint → root cause (Ingi: Option 3).** `VariableNotFoundException` now names the variable and points at the unset/unreachable cause, at the resolution layer where a `%var%` actually fails — not a downstream convert decline. (`ICreate` decline hint I first added was reverted per that decision.)

**B — tests re-homed to the self-conversion doors, consolidated in `Types`:**
- `DictListToRecordTests` — one dict→record suite over `dict.Clr` → reflection slots door; folded in the Runtime (`TypeMappingDictConversionTests`) + Modules (`StepFromDictConversionTests`) dict suites.
- `CreateDeclineMessageTests` — bind-decline messages over each type's `Create` decline.
- `ResidualTryConvertTests` → `item.Clr` / `list.Clr`.
- `PathTypeMapperTests` → `path.Create` scheme dispatch.
- `ValueConversionHookTests` trimmed to its unique non-door coverage (locale guard, `Image.Create`, `GoalCall.Convert`).
- Deleted dead-format message tests (`TypeMismatchMessageTests`, `TypeMismatchExample`) — redundant with the new reporters.
- `TypeMappingTestFacade` `ConvertTo`/`TryConvertTo`/`CaseInsensitiveRead` retired.

## Verification
All touched classes green in isolation. **Zero new deterministic failures** — attributed the pre-existing reds against a clean-HEAD baseline (identical counts): StartGoalTests 2, SnapshotWireTests 11 (deferred snapshot-wire redesign), RegistryFold `Get("json")→JsonNode` (json not registered; outside my diff). Full-parallel counts are the known crypto-under-parallelism flake; failures are scattered, none cluster on the change.

## Decisions taken (Ingi)
- Defer scalar-wrap-into-list (`5 → [5]`) — park; build on the list kind when a real site needs it.
- `%var%` hint at the resolution layer (Option 3), not the `Create` decline.
- Message wording: "variable", not "reference".
- Trim redundant door-centric tests rather than hollow-rewrite.

## Blocked — written up for architect
**C (converter strip) can't finish.** 12 scalar converters are dead (strip clean), but `dict.Json` is load-bearing for the STJ `application/json` MIME serializer (`channel.serializer.Json` — the *default*), also used by debug display, snapshot-clone, `set type=json`. Stripping the converters doesn't remove that path; the path is the *serializer*. This is a real two-path fork (STJ `application/json` vs `item.Output`/`data.Normalize` wire). Reverted C; tree clean at A+B. See `coder/stage2-converter-strip-blocked-on-json-serializer-fork.md`.

## Handoff inaccuracies found (for the record)
- The test inventory undercounted: the door had test-only callers beyond the listed 14 — `ValueConversionHookTests` (~20 `app.Type.Convert` sites), `ResidualTryConvertTests`, `SnapshotWireTests`. All re-homed. (Production callers were genuinely zero.)
- The demolition list said `dict/Json.cs` dies "after A's firing sites are gone" — it missed the live `channel/serializer/Json.cs:57` consumer.

## What's left
1. **Architect ruling** on the `application/json` serializer fork → then finish C (strip 12 dead scalar converters + type-entity converter + `dict.Json`, collapsing the STJ path onto the wire, or amend the plan if the STJ `application/json` serializer is a keeper).
2. **(Deferred)** scalar-wrap-into-list on the list kind, when a real site needs it.
