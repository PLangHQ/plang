# Pre-parsed variables → build-time value transforms

/ Status: design direction captured mid-exploration. Nothing is being built yet. Supersedes the perf/span framing in `../proposal.md`.

## Why

The branch began as two ideas started at the same time and entangled: a perf idea (cache `%var%` spans in the `.pr` so the runtime stops scanning) and a piping idea (give variable references structure so transforms can ride on them). Both were framed the same way — "the `.pr` stores a parsed form of the variable string." On review the perf/span half is a no-go, and the piping half is the real idea — but it is not about storage. It is about the builder compiling natural-language value-transforms into navigation expressions. This doc records the verdict and the design direction so they survive the branch switch.

## Verdict on the original proposal (perf + spans)

No-go on storing parsed `%var%` spans in the `.pr`.

The premise — "the builder already walks parameter strings to validate variable names, so the spans are free to harvest" — is false. Checked against the merged base (`BuildStep/Start.goal`, `BuildGoal/Validate.goal`, `PLang/app/builder/`): the builder does zero `%var%` parsing. It validates compiler errors, action errors, step counts, and now confidence. There is only one parser today — the runtime — and storing spans would create a second one in the builder plus a permanent sync obligation, while the runtime scanner stays for old/hand-edited `.pr`. Spans are also regex-recomputable, byte offsets are brittle, and a stale `.pr` carries a frozen parse the runtime cannot detect.

The principle that decides this whole area: **store derived data in the `.pr` only when re-deriving it needs the LLM.** Spans fail that test (a regex rederives them). The piping idea passes it (an LLM inference produced it). That single rule is why one half is dead and the other is alive.

## The real idea: build-time value transforms

A developer writes a step in natural language that references a typed variable and asks for something the type can do. At build time the compile LLM is fed that variable's type surface — the properties and methods it exposes. The LLM maps the intent onto a real member and writes the parameter value as a navigation expression. The runtime executes it deterministically. The natural language is parsed once, at build; the runtime stays deterministic.

Example: `- set %thumb% = %photo% resized to 200x200`. The builder knows `%photo%` is an `image` with a `Resize(w,h)` method, so it compiles the value to `%photo.Resize(200,200)%`.

This is why it passes the principle: "make it uppercase" → `text.upper`, "resized to 200x200" → `Resize(200,200)` is an LLM inference. The runtime cannot rederive it without re-running the LLM, which PLang refuses to do at runtime. So the `.pr` is the only place that mapping can live — and storing it there is correct, not redundant.

## How it lands in the `.pr`

It barely touches the `.pr`. Navigation already executes chained method expressions (`%data.grep("x").maxLength(100)%`) returning Data. So the transform rides inside the existing parameter `value` field as a navigation expression, and `formal` already renders it for the developer to review. No new `variables[]` block, no side-channel keyed by path (that would re-create the drift problem the spans had). One source of truth: the expression string in `value`. The `type` stamp on the parameter stays honest because the builder knows the input type and the member's return type (this leans on the typed-returns work already merged).

The feature is therefore mostly a builder change (feed the type surface, emit navigation expressions as values) plus making the navigable method set extensible instead of a hardcoded switch. The `.pr` shape and the runtime navigator are already there.

## The two execution paths — and the two rules that keep them clean

Enabling this surfaces two ways to execute, which the developer already has via variable navigation: the value's own property/method path, and the module.action path. They are different in kind — a type method *transforms the value* (`image.Resize`), an action is *a verb the actor performs in the world* (`storage.upload`). Two rules keep "two paths" from becoming "two ways to say one thing":

1. **One capability, one home.** A thing is either a navigable type member or a module action, never both. If `image.Resize` is navigable there is no `image.resize` action.
2. **Only pure, value-returning members are navigable.** `Resize` returns a new image — fine. `Delete` is an effect — that must be an explicit action step, because a value is resolved repeatedly and resolution must stay safe.

On the routing tie-break (when both could match): if a static priority is ever needed, the value-method should win, not the action — the method that takes the value and returns the same kind is the precise match; action-priority pulls the LLM toward the looser choice. Better than a static rule: show both surfaces, let confidence sort it, and a genuine tie becomes a low-confidence build event the developer resolves in review. Deferred either way — see open decisions.

## Open decisions

- **What is navigable** — deferred by Ingi. Needs a way to mark which type members are part of the PLang navigable surface; the same marker bounds what the builder shows the LLM. Opt-in per member vs convention (all public value-returning members navigable unless marked otherwise) is the fork.
- **Routing tie-break** — confidence-driven preferred over static priority; if static, value-method wins over action.
- **The collection-query frontier** — `where / select / orderby / take` on lists is the most powerful and the riskiest part. It turns the navigable surface from a handful of string helpers into a real query language. Worth its own pass before committing.
- **Purity boundary enforcement** — how "pure, value-returning" is actually guaranteed for navigable members.

## Examples

The full ladder (strings, collection queries, numbers/dates, files/images, control-flow composition, discoverability, and the ambiguity-into-review case) with real `.pr` snippets: see [examples](plan/examples.md).

## Status

Nothing is being built. This is a captured direction, not a stage plan. The orphan `os/system/modules/MapVariables.goal` and `MapVariablesSystem.llm` are stale sketches of the same instinct (variable → pipeline) — unwired, no callers — left in place pending Ingi's call on deletion.
