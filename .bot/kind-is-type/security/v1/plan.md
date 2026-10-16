# security v1 — kind-is-type (partial, architect-scoped)

Branch: `kind-is-type` (tip `96e22e675`). Parent: `runtime2` at `cf66e2d01`.

Scope the architect handed me (not the whole branch — coding isn't finished):

1. **Stage 0 (fcaf1055d)** — a goal's path is where its `.pr` is found. Closes decision 745's trust hole where a `.pr` could claim `/system/` via its stored `"path"`. Verify stage 0 actually closes every door, not just the renamed one.
2. **767 (073cf8080)** — `goal.list.Find` refuses `..` segments. Verify, then look for other doors that compose a name into a path.
3. **789/790 (0812b7bbf, 62f08e5e6, 7fc410084)** — settings read from their stored record; no reader for old shape; an unreadable row named loudly. Check nothing reads a row into the wrong class and no permission row can be misread as granting more.
4. **The decider-key resave** — a C# harness (deleted) + `test/SetDeciderKey.goal`. Confirm nothing secret landed in the repo or its history from this branch.

Approach: read each surface against the parent-branch baseline, trace what the change actually did, probe the obvious bypasses. Static only; no mutation unless a finding needs confirming. Semgrep as step 1.

## Findings (preview — written up in `result.md`)

- **F1 (High)** — Stage 0 closed the `"path"` door but opened a new one. `goal/serializer/Reader.cs:96` reads the `.pr`'s `"file"` and does `pr.Parent.Parent.Combine(file).Relative(ctx.Context)`. The `file` string is used verbatim; a `.pr` whose `file` is absolute (`"/opt/plang/os/system/evil.goal"`) or climbs (`"../../../os/system/evil.goal"`) sets `goal.Path` outside the `.pr`'s own folder — canonicalisation in the FilePath ctor resolves the climb, which is the point. `AskedByOs` then trusts the smuggled goal silently. Same trust-hole class 745 named; the `"file"` door is new in this stage.
- **F2 (verified clean)** — 767's `..` block is sound; the other goal-name-to-path doors route through `goal.list.Find` or take a resolved path, not a composed name.
- **F3 (verified clean)** — 789/790 settings class discipline: `setting.Create` only constructs non-abstract `setting`-subclasses off `declared.ClrType`; the record reader keeps the owner's own class; permission ride-the-signed-wire discipline is untouched.
- **F4 (verified clean)** — no secret landed. The one `.sqlite` fixture touched by the branch was added and then deleted on the same work; its content is reproduced in the report and holds an empty `key` string. No `tsk_*`, `sk-*` or `/shared/hopkaup/secrets/*` in any diff.

Tooling: `scripts/semgrep-scan.sh` (24 blocking hits, baseline-known: 22 serializer-hygiene + 1 gated Assembly.LoadFrom + 1 Display.Gate false positive; nothing new).
