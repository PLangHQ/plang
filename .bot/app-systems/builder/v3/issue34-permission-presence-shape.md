# Issue 34 — permission presence gate — shape (builder → architect, 2026-10-02)

**Bug (os bot, b12d1ef29):** `start //bin/sh with X, it may read X, and read and write /granted, write
to %said%` built a `terminal.start` with **no `Permission`** — the program would run free though the
step named its permissions. A word-match check (`may`, `permission`) reads the step's words — banned.

## The structural fix: a presence-gated Option on `Permission`
This is a third mode of the Option question, alongside the two the pick pass already has (the offer
comes from the property's **type**, per the architect's earlier design):

| mode | property type | `question.Values` (offers) | Prefill writes | Agree |
|---|---|---|---|---|
| **choice** (Template) | a closed set | its values + `none` | `Prop=value` | — |
| **variable** (Item, Key, Conversation) | a free/ref type | the step's placeholders + `none` | `Prop=%chosenVar%` | — |
| **presence** (Permission) — NEW | a complex type (`{path, verbs}`) | **`given` + `none`** | **nothing** | **requires the property when `given`** |

### How the yes is asked
`Permission`'s type offers a two-value choice **`given` / `none`** (a yes/none), rendered by the
existing `decider2.template` `when "Option"` case (it already renders `q.Values` as a choice — no new
template branch). The `ask:` note asks it: *"does the step say what the program may read or write?"*
The decider answers `given` when the step names read/write; `none` when it doesn't. It is **intent**,
not a word-match: the decider reads the step's meaning against that question, which is allowed (the
banned thing is a C# check grepping the step text).

### Where the presence rule lives — core, pick's `Agree`
`Agree` (`PLang/app/goal/step/pick/list/this.cs` ~l.219) already refuses a `Mark.Certain` action the
answer leaves out. Add the twin: **for each Option answered a non-`none` presence value (`given`),
the built action must carry that property; if absent, refuse** — "the step says what the program may
read or write, so `terminal.start` must set `Permission`." `FixSteps` retries with that reason, as it
does for a left-out certain action. (Core; I shape, the coder builds.)

### No value to prefill
Unlike `Template=plang` (a literal) or `Item=%x%` (a chosen variable), `given` has **no value** to put
in the `=> formal:` line — `Call`/`Prefill` append nothing for a presence-gate. `_option[{m}.{a}.
Permission] = "given"` is a *presence signal*, not a value. The writer builds the actual
`Permission = {path, verbs}` from the step words, helped by the permission type's `Example`/`Shape`/
`Description` the coder is adding (so the model sees `{path, verbs}`, fixing try-2's flat list).

## Split
- **Core (coder, after architect review):**
  - the permission type's `Offers(step)` returns the `given`/`none` presence choice, marked as
    *presence-only* (no value) so `Call`/`Prefill` skip it and `Agree` enforces it;
  - `Agree`'s presence rule (refuse a `given` option whose property is absent from the built action);
  - the permission type's `Example`/`Shape`/`Description` (`{path, verbs}`) — already queued.
- **Builder (mine):** the `ask:` line on `terminal.start`/`terminal.open`'s `Permission`
  ("does the step say what the program may read or write?") + the per-action teaching. (Lands when the
  terminal module reaches app-systems — it's not here yet; the shape is module-agnostic and reusable
  for any action with a must-be-present-when-named option.)

## Open question for the architect/coder
Is the presence-gate a **distinct `Kind`** (e.g. `Kind.Presence`) or **`Kind.Option` with a
given/none Values + a type-supplied "presence-only" marker**? I lean on keeping it in the **type**
(the type says "I offer presence, not a value"), so the question stays `Kind.Option` and only `Call`
(skip prefill) and `Agree` (enforce) branch on the marker — no new Kind, one place each.

---

# Issue 35 — `//bin/sh` → `/bin/sh` — NOT path normalization

The same step turned `//bin/sh` (OS-absolute) into `/bin/sh` (app-rooted) → `ProgramNotFound`.

**Ruled out: the path type.** On Linux `IsOsRooted("//bin/sh")` is `path.StartsWith("//")` → **true**
(`PLang/app/type/item/path/file/this.Validate.cs:88-92`), so `ValidatePath` **leaves the `//` intact**
(`:39-43`: "Leave the // prefix intact for idempotency"), and `Canonicalize` preserves it too
(`PLang/app/type/item/path/file/this.cs:41`). So path resolution does **not** strip the slash.

**Therefore it's upstream of the path type:** the **writer** (the Properties LLM normalized `//bin/sh`
→ `/bin/sh` when writing the `Program` value — a common model normalization), or the terminal module's
own handling of `Program` (the module isn't on app-systems yet, so I can't build it to tell them
apart). **The determining check (for the os bot, who has the b12d1ef29 build):** read the raw built
`.pr`'s `Program` value — if it is already `/bin/sh`, the writer dropped the slash (builder/teaching
fix: teach `//` is OS-absolute and copied verbatim); if the `.pr` holds `//bin/sh` but it runs as
`/bin/sh`, it's the terminal module resolving `Program` through the wrong path form (core). The path
type is cleared either way.
