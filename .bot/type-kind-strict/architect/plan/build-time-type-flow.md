# Build-time type flow — the settled model

The agreed picture, on one page. Stage 8 maps it to code; this is the *what* and *why*.

## The spine — the runtime remembers, the LLM is told

Every step compiles as an **independent** LLM call with **no memory** of the steps before it. So the LLM compiling `verify %bla%` cannot know `%bla%` is a hash on its own. Cross-step type knowledge lives in the **builder runtime** (deterministic C#/PLang): it walks the steps it has already built, knows `%bla%` holds a hash because step 1's action returned one, and *feeds* `%bla% (hash)` into step 2's prompt.

The LLM never accumulates state — it is handed exactly the slice it needs, freshly, each step. Because the prompt is rebuilt per step, it should contain **only what that step needs**. The runtime is the memory; the prompt is the message.

## Two categories of fundamental type

PLang is higher-level than C# — it is *for* files, media, web, AI — so `image`, `audio`, `video`, `path` are **fundamental types in the language**, not library types. The fundamentals split by one question:

**Can you write the value inline in a goal, or only a reference to it?**

- **Inline fundamentals** — the value can be a literal in the goal text: `text, number, bool, object, list, dict, datetime, date, time, duration, guid`. The LLM assigns these by looking at the written value (`5` → number, `true` → bool, `"hi"` → text).
- **Reference fundamentals** — you can only write a path/handle, never the data: `image, video, audio, path`. There is no image literal in a goal; you write a path. The value's type is **declared** (`as image`) or **produced** by an action (`read`). The LLM never tags a raw literal as one — there is nothing to tag.

Both groups are always-on in the small vocabulary (~16 names). The split is not cosmetic: it's exactly why the kind-parsing rule (rule 1) fires only for the reference group — a reference value carries a path whose extension is a real format signal; an inline literal's spelling is just characters. (`bytes` is borderline — base64 is sort-of-writable — but behaves like a reference fundamental.)

### Reference fundamentals are lazy handles at runtime

The "you only write a reference" shape carries through to runtime. `- set %x% = "file.jpg" as image` does **not** read the file:

1. `variable.set` runs, sees declared `type = image`.
2. Mints an `image` value, sets its `.Path = "file.jpg"`.
3. Returns it. **No I/O.**

`%x%` is a lazy handle — it knows *where* it is, not yet *what* it is. The bytes/decode/dimensions materialize from `.Path` only when something needs the content (a later step that reads width, pixels, etc.). This is the same lazy philosophy as file-read-returns-raw; an `image` is essentially a `path` plus lazily-decoded content, and holding one costs nothing. (What happens when a step *mutates* the loaded content — and how the value then diverges from its backing file — is deliberately out of scope here; parked.)

Implementation: [stage 8 is build-time; the runtime lazy handle is stage 9](../stage-9-lazy-reference-handles.md).

## The four rules

1. **Kind source.** A type's `kind` comes from exactly two places: the developer's explicit `as name/kind`, or a **producing action's `Build()`** reading the real content/path. **Never** inferred from a bare literal's spelling. (`read file.md` → `{text, md}` is true — the value *is* markdown. `set %x% = "file.md"` → `{text}` — the value is the 7-char string "file.md", which is not markdown.)

2. **Bare literal → value-shape type, no kind.** A literal with no explicit `as X` takes the type its written value implies (`"hi"` → text, `5` → number). Enforced deterministically, not LLM-guessed. The set path parses the value into a kind **only when a non-`text` type was declared** (`name != text`, i.e. a reference fundamental the developer named) — `set "file.jpg" as image` → parse path → `{image, jpg}`; `set "file.jpg"` → `{text}`.

3. **Per-step prompt = small vocabulary + this step's action types + runtime-fed in-scope types.** Always-on: the ~16 fundamentals (so the LLM can tag literals and recognise a developer's `as image`). Plus: the specific types the current step's actions reference (their params/returns). Plus: the types of variables in scope, fed by the runtime. **Never** the full catalog of every module's records/enums/result types — those arrive deterministically (by return, by `Build()`, by scope), so the LLM never needs to choose them.

4. **Type introduction.** A type enters a build on an **action's return**; the action's `Build()` may **refine** that return to something sharper than its static shape (static floor `object` → `Build()` sharpens to `{image, jpg}`). The developer never *declares* a result type like `hash` — `crypto.hash` returns it, and that return is what makes it known to the runtime, which feeds it forward.

## When does the LLM need the catalog at all?

Only when **it must choose a type**. Walk the cases and most aren't that:

| Step | Who decides the type | Catalog needed? |
|---|---|---|
| `read file.cer → %c%` | the action's `Build()` parses the path | no |
| `hash %x% → %bla%` | the action's return | no |
| `verify %bla%` | runtime feeds `%bla% (hash)` from step 1 | no |
| `set %x% = 5` | LLM tags a literal → small vocabulary | small set only |
| `set "f.jpg" as image` | developer named it → LLM echoes (grounded by the small set) | small set only |

The full catalog is never the thing the LLM picks from. Grounding `image`/`video`/`audio`/`path` in the always-on set is what makes a developer's `as image` reliable recognition rather than prose-guessing — that's the argument *for* keeping them always-on.
