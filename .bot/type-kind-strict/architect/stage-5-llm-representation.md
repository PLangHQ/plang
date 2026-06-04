# Stage 5: Restructure the type information sent to the LLM

**Goal:** Collapse the three disagreeing type surfaces into one — a cached vocabulary block in the system prompt (generated from the catalog, two render modes), a slimmed per-step block (domain/record types only), and the `type` parameter taught as a constructor.
**Scope:** Included — `PLang/app/builder/type/this.cs` (`TypeSchemas` renderer + a vocabulary view), `os/system/builder/llm/Compile.llm` (replace the hand-written list), `os/system/builder/llm/CompileUser.llm` (drop the flat `Primitive types:` line), the `type` entry's description, and removing the `as text` prose from `variable.set`'s Notes. Excluded — none; this is the last stage.
**Deliverables:** A catalog-generated type vocabulary in the cached system prompt; `TypeSchemas` rendering advertised kinds (`(kinds: …)`) vs extension-derived kinds (`kind = extension (…)`); the per-step block carrying only step-specific domain types; `type` taught as `type(name, kind?, strict?)`; the `as text` prose folded into the `type` entry.
**Dependencies:** Stages 1–4 (the type model, `text`, kinds, and `variable.set` must exist before the prompt can describe them).

## Design

See [plan/llm-type-representation.md](plan/llm-type-representation.md) for the full narrative and the captured before-state. Key points:

- The catalog `Entry` struct already dissolved into `app.type.@this` (singular-namespaces merge), so `TypeSchemas` (on `app.builder.type.@this`, `PLang/app/builder/type/this.cs`) now renders from `type.@this` entities reading their folded `Fields`/`Values`/`Kinds`/`Shape`. The descriptor↔catalog unification is done; the *prompt* restructure is what remains.
- **Move the universal vocabulary to the cached system prompt.** The core type names + kinds are goal-invariant. Generate them from the catalog (`app.builder.type.@this` / `app.type.list.@this.BuildTypeEntries`) into `Compile.llm`, replacing the hand-written valid-type list. Generating it means it can't drift from the per-step block again.
- **Slim the per-step user message.** Remove the flat `Primitive types:` line from `CompileUser.llm` — its content now lives in the cached vocabulary. Keep the scoped `Catalog types referenced by this step's actions:` block, but it now carries only domain/record/enum types (`path`, `llmmessage`, enums, records).
- **Two render modes in `TypeSchemas`.** Advertised (`type.@this.Kinds` populated, e.g. `number`) → `name — kinds: a | b | c`. Extension-derived (a `Build` hook discoverable via `app.type.kind.@this`, no `Kinds`, e.g. `text`/`image`) → `name — kind = extension (examples)`. Both signals already exist on the entity; branch on them.
- **Teach `type` as a constructor.** `type(name, kind?, strict?)` — emit `name` and `kind` separately, never the `text/md` slash form. The three paragraphs of `as text` prose in `variable.set`'s Notes collapse into the `type` entry's own description, taught once wherever a `type` param appears.
- **Confirm the catalog shape for `type`** (record `{name, kind, strict}` vs scalar with a constructor signature). The record path is likely right because the LLM should emit a dict here — but the existing `TypeSchemas` note warns the verbose scalar form misleads the LLM into emitting a dict, so verify against a real build trace before settling. The intent: the LLM emits `{"name":"text","kind":"md"}`.

Validate the result the way this plan was researched: force a fresh compile (`plang '--build={"files":[...],"cache":false}'`) of a goal that references `text`/`number`/`image`, read the new trace under `.build/traces/<id>/`, and confirm the rendered vocabulary and the `type` entry read the way the LLM needs.
