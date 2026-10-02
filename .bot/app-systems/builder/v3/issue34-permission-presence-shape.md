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

### Where the presence rule lives — core, pick's `Agree` (GENERIC — architect, refined)
**One rule for every option, not a presence-special case:** any option the decider answered with
something other than `none` **must be present in the built action, or it is refused** — a chosen
`sha256` missing `Algorithm`, a chosen `plang` missing `Template`, a `given` missing `Permission`, all
refused the same way. `Agree` (`PLang/app/goal/step/pick/list/this.cs` ~l.219) already refuses a
`Mark.Certain` action left out; this is the twin over `_option`. `FixSteps` retries with the reason.
So **no `Kind.Presence`, no type marker for Agree** — one generic rule.

### No value to prefill — the type answers how its offer enters the line (no Call fork)
`Call`/`Prefill` must **not** branch on a presence flag. Instead the **type answers how its chosen
offer enters the formal line**, the same way it already answers what it offers: a choice's value
writes `Name=value` (`Template=plang`), a variable writes `Name=%x%` (`Item=%field%`), and the
permission list's `given` writes **nothing** (a presence signal). `Call` asks the type and has no
branch. The writer builds the actual `Permission = {path, verbs}` from the step words, helped by the
permission type's `Example`/`Shape`/`Description` the coder is adding (so the model sees
`{path, verbs}`, fixing try-2's flat list).

## Split (architect-finalized — one generic rule, type answers the rest; goes to the coder after the offers batch)
- **Core (coder):**
  - the permission type's `Offers(step)` returns the `given`/`none` presence choice (just another
    type answering its offers — no `Kind.Presence`, no marker);
  - the type answers **how its chosen offer enters the line** (value → `Name=value`, variable →
    `Name=%x%`, `given` → nothing), so `Call` asks the type and does not fork;
  - `Agree`'s **generic** rule: any option answered non-`none` must be present in the built action, or
    refuse (covers Template/Algorithm/Permission alike);
  - the permission type's `Example`/`Shape`/`Description` (`{path, verbs}`) — already queued.
- **Builder (mine):** the `ask:` line on `terminal.start`/`terminal.open`'s `Permission`
  ("does the step say what the program may read or write?") + the per-action teaching. **Lands when
  the terminal module reaches app-systems** — not here yet; the shape is module-agnostic and reusable
  for any action with a must-be-present-when-named option.

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
