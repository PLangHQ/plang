# codeanalyzer v2 — kind-is-type (stages 4–6 + 814 + stage-5 C# + security F1)

## Scope (architect, decision 847)

OBP review of everything landed **after my v1 pass** (`82d8866ae`) up to HEAD `aa353656b`:
- **Stage 4** — a format type owns its bytes: json rests / a use opens (332911aa8, 00eab2f6a, 846315a9c, 187d6040c), render (7574cd6d3), the navigator deleted (2622f5514), the text-of-kind-json wrapper + coiner gone (d6d92efc2), `Kind(string)` by-name gone (38077d61c), llm.query with a Schema (d61eea852), the `is` operator (8fc6e1b13).
- **814** — host kinds are `Internal` via a new `OwnInternal` hook (ff2c44069); file.read's Type slot (f249fe1cb, e06f57518, 829f78e8e).
- **Stage 6** — the clr stopgap + my v1 F1–F3 (5b19121e6); F4.2 `name<kind>` face (8cc2a5c65).
- **Stage 5** — the builder's production C# + teaching (0fda873ec, d3138cf16, 50e83a808).
- **Security F1** — `d9bc6aae7` (read for shape only; already re-checked by security).

## Deferred — NOT findings to re-raise (architect / coder handoff)
- **F8.1** — the three "is this an action slot?" name checks (branch A's list).
- **#3 the floor** — `type/item/source.cs:115` `_type.kind.IsText || _type.Name is "text"` (branch A's list; removing it regresses ~13 template/file tests — needs branch A's "one make door").
- The **6 Wire snapshot/resume tests** (fail identically on runtime2).
- **`After_EachRetry_IsAFreshAttempt_WithAFreshDeadline`** (timing under load).

## Method (as v1)

Read each touched type in full, walk callers upstream; name findings by the `obp-smells.md` catalog names; a compiler-backed hide sweep for the locked core; force the build (`rm -f .devsh-stamp*; ./dev.sh build`). `Tools/ObpScan`'s MSBuildWorkspace can't load the csproj here (NuGet prune/vuln warnings) — substitute full-read + grep + the real compiler.

## The v2 OBP crux

The "json rests, a use opens" shape must be **one door**, not open/parse logic copied across the 6+ use-sites (broken-seal / stored-twice). Plus: the `OwnInternal` hook on the locked `type` surface (did it keep the `Own*` pattern and stay loop-free?); file.read's Type slot (does it crack a carrier into primitives — opened box?); no new app-wide by-name kind search; no new CS0108 hide in the core (the 776 discipline).

## Output
`v2/{plan.md, report.md, verdict.json}`, update `summary.md`, append a session to `report.json`; commit, pull --rebase, push. Reply: verdict + hash + each finding's id/smell/file:line. Read-only; a locked-surface question goes to the architect, not a fix.
