# codeanalyzer v1 — app-systems pre-merge OBP scan

**Traces to:** architect brief `.bot/app-systems/architect/briefs/codeanalyzer.md` + `common.md`; Ingi decisions 665 (bar: every known failure owned), 689/651 (locked core), 717/718 (baseline), 721 (this review).

## Task

OBP scan of the locked core's diff since runtime2 (`0ea5a4b94..app-systems`), then the builder pipeline. 977 C# files changed; no OBP pass since 06-07. Read-only — a fix that changes a locked surface is a **proposal**, never an edit.

## Method (per brief + `Documentation/v0.2/obp-scan.md`)

For each area: `git diff 0ea5a4b94..app-systems -- <path>`, read each touched type in full (not just the diff), run `dotnet run --project Tools/ObpScan -- <type>` on changed types, follow the triage order (fork → unplanned API surface → verb+noun → shape smells → value smells → meta-test).

Weighted highest (found live on this branch in last 2 days): exception-as-answer, stored-twice, "is X?" by name/type, clr leak, reflection.

**Carry discipline:** before raising, check `next-branch.md`, `obp-cleanup.md`, `summary.md` decision log. If present → cite "carried: …", don't re-raise.

## Areas (one report section each, resumable)

1. data and the wire — `PLang/app/data/`, `data/Wire.cs`  ← **in progress**
2. channel and stream — `PLang/app/channel/`
3. core value types — `PLang/app/type/item/{text,number,bool,list,dict,path,datetime,duration}/`
4. the type system — `PLang/app/type/this.cs`, `type/list/`, `type/kind/`, `type/property/`, `type/item/this*.cs`
5. the serializer — `PLang/app/type/format/`, `type/serializer/`, `type/item/wire/`
6. call & variable memory, events — `PLang/app/call/`, `type/item/variable/list/`, `PLang/app/event/`
7. module registry & app — `PLang/app/module/this.cs`, `PLang/app/this.cs`
8. builder pipeline (new, +4.2k) — `PLang/app/goal/step/`, `PLang/app/module/build/`

## Output

Per finding: smell name (CLAUDE.md names), file:line, one sentence wrong, fix direction, **blocks / carry**, **surface change: yes/no**. Section per area to `v1/report.md`; verdict to `v1/verdict.json`; session to `.bot/app-systems/report.json`. Commit + message architect after each area.

## Environment note

Running in an isolated worktree `/workspace/plang-app-systems` (branch `app-systems`) — the primary `/workspace/plang` checkout is shared with ~14 live sessions and must not have its branch switched. ObpScan runs against a restored `PLang.csproj`; MSBuild emits NU-prune/vuln load warnings but still renders the member tables.
