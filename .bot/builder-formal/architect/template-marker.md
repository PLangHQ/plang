# Render only what is marked a template

For coder, after a restart (the session that got the earlier message is gone). Ruled by Ingi, 2026-09-26.

## Why

Ingi: "string guesses from its content — that is really bad and should be completely removed; the only way it should try to render a var is when template is set."

Reading a value today turns ANY string holding `%x%` into a template when its row carries no marker (`type/this.cs:315`). Every store read-back goes through that arm: `.pr` goals, the settings store, the LLM cache. So a text from outside that happens to hold `%x%` (an LLM answer, a cached response, a saved value) gets rendered against live variables. That's the crash coder hit on a cached answer, and it's a template-injection path. The builder already writes the marker: every row whose value holds a variable has `"type": {"name": "text", "template": "plang"}` (show.pr 8×, start.pr 48×). The marker is the truth, and nothing should infer it.

## The rule

- **A value renders `%var%` only when its type carries `template`.** No marker means the text is plain, whatever it contains.
- **The marker is born in one place:** at BUILD, when the formal reader types the programmer's literal (content decides there, because it's the programmer's own text). Nowhere else.
- **An explicit request is a birth fact:** `file.read … ResolveVariables=true` returns the file item born with the template marker; its text renders itself when read. Open for Ingi: whether that template may read `%!x%`.

## The sweep (file:line, read on 2026-09-26)

| Where | Today | Change |
|---|---|---|
| `PLang/app/type/this.cs:312-316` (string arm) | `if (Template == null && text.HasVariable(slice))` → stamps `"plang"` | **Delete.** The slot is exactly its row's declared type. |
| `PLang/app/type/this.cs:328-334` (container arm) | `ctx.Template != null && Template == null && HasVariable(raw)` → stamps the container | **Delete.** A container is a template only by its own row's marker. |
| `PLang/app/module/action/file/read.cs:105` (Build) | `raw.Contains('%')` → "can't judge at build" | Ask the marker: `Path.HasVariableReference` (`data/this.cs:132`). |
| `PLang/app/module/action/http/HttpBuildHelpers.cs:17` | `raw.Contains('%')` | The same: the marker. |
| `PLang/app/module/action/llm/query.cs:123`, `:127` | `HasVariable(st.ToString())`, `format.Contains('%')` | The same: the marker. |
| `PLang/app/module/action/file/read.cs:68-80` (ResolveVariables) | opens the content (`read.Value()`) and calls `Context.Variable.Resolve(content, skipInfrastructure: true)` | **Ingi:** the handler never opens what it returns. It returns the plain lazy file reference, **born with the template marker**; the file's text content is born a template when read and renders itself at use. |
| `PLang/app/variable/list/this.cs:445-478` (`Resolve(string, bool)`) | the store renders any string it's handed | **Deleted.** The store answers `Get(name)`; the render moves onto text (Value/Output); `source.Output` hands over to a text. |

**Stays (checked):**
- `json.cs:68-71` StringSlot and `:190-197` TextLeaf: gated on `ctx.Template`, which comes only from a type's marker (`wire/this.cs:25`, `source.cs:196`). Inside a marked container, stamping only the slots that hold `%x%` keeps literal slots canonical.
- `text/this.cs:204`: the constructor NARROWS a given mark (a literal without `%x%` drops it). It never adds one.
- The renderers `text.Value` (:104), `text.Output` (:135) and `source.Output` (:262) are already gated on `Template`.
- Build-time parsing of the programmer's words: `Formal.cs`, `step/this.Scope.cs`.

## The 16 tests that broke on the first try

Gate on the ROW's marker, then list what still fails. A fixture row holding `%x%` without the marker is stale: regenerate it through the writer; never restore the guess. Anything else comes to the architect.

## Tests

- A store read-back of a text holding `%name%` with no marker stays text (no render, no VariableNotFound).
- A `.pr` row with the marker renders; the same value without it doesn't.
- A cached LLM answer holding `%name%` stays text (`QueryCacheTests`, already green).
- `file.read … ResolveVariables=true` still renders, and `%!x%` stays literal.
- file.read/http/llm.query Build: a marked value is skipped at build, and an unmarked literal holding `%` is judged.

## Demolition

- The string-arm and container-arm guesses in `type/this.cs` (both deleted).
- `content.Contains('%')` / `HasVariable(raw)` as a render or skip decision anywhere outside `text`'s own constructor and the json container slots.

## OBP validation

| Surface | Check | Result |
|---|---|---|
| the template marker | one owner of "is this a template" | the type's `Template`, born at build |
| `HasVariableReference` | reuse, no new member | exists (`data/this.cs:132`) |
| file.read ResolveVariables | no new concept | stays a direct, guarded render on explicit request; no second template mode |

Then: update `Documentation/v0.2/wire-serialization.md`'s section to this rule (replace the "authored wire" proposal). Six suites vs baseline; commit and push.
