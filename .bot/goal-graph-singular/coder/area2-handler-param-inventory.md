# Area 2 — handler-param singular sweep: inventory (dispositions before any rename)

Branch `goal-graph-singular`. Per the plan: *"the inventory commit lists them ALL with per-name
dispositions before any rename."* This is that commit. Nothing renamed yet — this is the worklist.

Rename carries the wire automatically (WireName = camelCase of the property; the generator regenerates
LLM bindings; catalog rows read property names). The exceptions are called out.

## A. LLM-facing handler params → singular (the catalog vocabulary the LLM authors)

| Action | Param | → | Type | Note |
|---|---|---|---|---|
| `http.request` / `download` / `upload` | `Headers` | `Header` | `Data<dict>` | 3 sites |
| `signing.sign` / `verify` | `Headers` | `Header` | `Data<dict>` | |
| `http.request` / `download` / `upload` | `DefaultHeaders` | `DefaultHeader` | `Data<dict>` | **borderline — reads oddly, see §G** |
| `mock.intercept` | `Parameters` | `Parameter` | `Data<dict>` | |
| `ui.render` | `Parameters` | `Parameter` | `Data<list>` | |
| `llm.query` | `Messages` | `Message` | `Data<list<LlmMessage>>` | |
| `llm.query` | `Tools` | `Tool` | `Data<list<GoalCall>>` | |
| `test.report` | `Results` | `Result` | `Data<list<test>>` | |
| `test.tag` | `Tags` | `Tag` | `Data<list>` | |
| `output.ask` | `Variables` | `Variable` | `Data` | |
| `error.handle` | `Actions` | `Action` | `Data<list<action>>` | recovery actions (`on error call …`) |

## B. Internal / build handler params → singular (D2 = handler params ALSO singular; host params, not LLM vocab)

| Action | Param | → | Note |
|---|---|---|---|
| `build.validate` | `Actions` | `Action` | host param (catalog-dropped) |
| `build.validateStepActions` | `Actions` | `Action` | host param |
| `build.types` | `Actions` | `Action` | host param |
| `build.promoteGroups` | `Steps` | `Step` | `Data` |
| `build.this` (`app.Build`) | `Files` | `File` | **USER-FACING CLI — see §G** |
| `build.actions` | `Actions` | — | **STAYS** (D3 — the action dies in module-discovery 6c; renaming a corpse isn't cleaner) |

## C. Model-type fields (not handlers, but plural param names in the vocabulary)

| Type | Field | → | Note |
|---|---|---|---|
| `GoalCall` | `Parameters` | `Parameter` | `List<data>` — mirrors `action.Parameter` (already renamed) |

## D. LLM schema keys — D1 (still plural; the wire-flip pass did the graph but NOT these)

| Site | Key | → |
|---|---|---|
| `os/system/builder/BuildGoal/Plan.goal:26` | `steps:` / `actions:` (schema string) | `step:` / `action:` |
| `os/system/builder/llm/Plan.llm` (examples) | `"steps"` / `"actions"` | `"step"` / `"action"` |
| `build/code/Default.cs` QueryAndVerify / FixValidation schemas | `actions:` | `action:` (and UNIFY the two while touched, per plan) |

LLM cache busts by content — expected, noted, not fought.

## E. Stale refs from PRIOR renames (sweep misses — fix regardless of §A–D)

- **`os/system/builder/llm/templates/stepForLlm.template:3`** navigates `a.Parameters` (plural) — but
  `action.Parameters → Parameter` already landed (`ed998671d`). So this template currently renders
  **zero params** in the stepForLlm view. Real latent bug — fix to `a.Parameter` regardless.

## F. STAYS (per plan — do NOT touch)

- `test.list.Include` / `Exclude` — plan: "Include/Exclude and every non-plural param name" stay;
  `Test.Include`'s typed-generic decl is the parked settings pass.
- `BuildResponse.Steps` / `Errors` / `Warnings` — **deferred** with the whole BuildResponse/Info
  conversion (D-C, gated on the recovery round-trip). Not this pass.
- `Data.Result.Warnings` — same (stays `List<Info>` until the later pass).
- All non-plural names (`Include`, `Exclude`, `Body`, `Cache`, …).

## G. NEEDS YOUR CALL (user-facing / semantic — I won't rename these without a nod)

1. **`build.Files → File`** — USER-FACING CLI. `Setting.Set` binds the JSON key to the property by
   name, so the rename *is* the flag change: `--build={"files":[…]}` → `--build={"file":[…]}`. The
   plan already flags this as intended + user-facing (cli_reference.md + help text update in the same
   commit). Confirm the singular `"file"` reads right for a multi-file flag, or keep `Files` as a
   deliberate CLI carve-out?
2. **`DefaultHeaders → DefaultHeader`** — a settings-default dict of headers. "default header"
   (singular) reads oddly for a bag of defaults. Rename for consistency, or carve out?
3. **`test.this.Tags` (`[Out]` on `app.test.@this`)** — the test entity's tag collection, not a
   handler param. Rename to `Tag` for consistency with `test.tag.Tag`, or leave (entity field, not
   vocabulary)?

## Non-targets (plural spelling, but not a collection-of-items param — leaving unless told)

- `crypto.hash.Bytes` (`byte[]`) — binary payload, not a list (cf. `RawBytes → binary` in the catalog).
- `llm.ToolCall.Arguments` (`string`) — JSON string mirroring OpenAI's `arguments`; internal LLM type.
- `debug.Variables` (`List<DebugVariable>`) — internal debug type, not LLM vocabulary.

## STATUS (updated as executed)

- **§A DONE** — `Headers→Header` (http request/download/upload, signing sign/verify), `Messages→Message`
  + `Tools→Tool` (llm.query), `Parameters→Parameter` (mock.intercept, ui.render), `Results→Result`
  (test.report), `Variables→Variable` (output.ask), `error.handle.Actions→Action`. All green; zero new
  test failures (baselined).
  - **CARVE-OUT: `test.tag.Tags` STAYS plural** — the action class is `Tag`, so a `Tag` member hits
    CS0542 (member name == enclosing type). Forced, like the documented `app.filesystem.Default`
    keyword carve-out. `DefaultHeaders` (§G) left untouched by design.
- **§C DONE** — `GoalCall.Parameters→Parameter` (consumers in this.cs / goal.call / http+llm providers).
- **§B DEFERRED** — `build.validate/validateStepActions/types.Actions`, `promoteGroups.Steps`,
  `build.Files`: all in build.code, entangled with the `using Actions = List<action>` alias, the generic
  `action.Actions` reads spanning the D3-surviving `build.actions`, and the blocked recovery/build area.
  Do as a group when that area is unblocked (the handoff's "leave role-2 alive until 6c").
- **§D, §G** — pending (schema keys / your rulings).

## Proposed execution order (after §G answered)
1. §E fix (the stepForLlm template bug) — independent, ship first.
2. §A + §C (LLM vocabulary) — carries the catalog + wire; verify ParamDescParity + catalog templates.
3. §B (internal/build params).
4. §D (LLM schema keys) — the compile-quality-risk piece; eyeball prompts vs the area-0 reference.
5. §G items per your ruling; CLI docs + help text in the same commit as `Files → File`.
