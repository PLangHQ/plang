# security v1 — result

**Branch:** `compile-llm-notes-per-action`
**Build:** not rebuilt this pass — branch is documentation/build-time
catalog plumbing only; the tester v2 verdict (clean rebuild, C# 2945/2945,
plang 204/204 + drift 2/2, 3 fresh-cache rounds) stands as the build
baseline for this audit.
**Audit scope:** the seven branch commits past the runtime2 merge
(`d8c18e257..55da8f529`) — production C# net delta is three files
(`MarkdownTeaching.cs`, `app/modules/this.cs`, `app/goals/.../action/this.cs`)
plus mechanical attribute deletions across ~130 action handlers.

**Verdict: PASS (0 new critical/high/medium/low; no new attack surface).**

The branch swaps the storage location of LLM-teaching prose from C#
`[Description]`/`[Example]` attributes (compiled into the assembly) to
repo-tracked `.md` files read at build time. Both sources are equally
trusted (repo content, developer-authored, version-controlled); both
flow into the same sink (the planner LLM's system prompt). The change
is mechanical equivalence — no new boundary crossed, no new sink
introduced.

---

## Trust model recap

| Element | Origin | Trust |
|---|---|---|
| `MarkdownTeachingRoot` | `App.OsDirectory` (process-level, set at startup) | trusted |
| `moduleName` arg to `Load` | `Names[]` (`typeof(...).Namespace`) | trusted (compile-time) |
| `actionName` arg to `Load` | reflection over `[ClassRoute]`-registered handler types | trusted (compile-time) |
| `.md` file content | repo-tracked, under `os/system/modules/` | trusted (developer) |
| Loader output sink | LLM compile prompt (build-time planner) | n/a — excluded from review per ruleset rule 14 |
| Orphan-scan output sink | actor `Output` channel (developer terminal) | trusted local |

No element on this branch is influenceable by an unprivileged caller
(remote origin, PLang program input, environment-untrusted user). The
PLang program-time surface is untouched.

---

## Per-item findings against the plan

### 1. Path traversal in `Load` / `ScanOrphans` — **not exploitable**

`MarkdownTeaching.Load(modulesRoot, moduleName, actionName)` calls
`Path.Combine(modulesRoot, moduleName)` then per-file
`Path.Combine(folder, $"{actionName}.notes.md")` etc.

Call sites:

- `app/modules/this.cs:393` — only call site. `moduleName=ns`, where
  `ns` iterates `Names` (the `Namespace` of compile-time-registered
  handler types). `actionName` is the per-handler action name from
  reflection.

Neither value is reachable from a PLang program, an HTTP request, or a
deserialized payload. The set of `ns` values is fixed at compile time
by the `Names[]` registry (`PLang/app/modules/this.cs:200`-ish); none
contain `..` or path separators. Even if a future contributor
registered a malformed namespace string, the worst case is reading a
file the build process already has trust to read (the repo working
tree) — there is no privilege boundary to cross.

`ScanOrphans` walks `Directory.EnumerateDirectories(modulesRoot)` then
`EnumerateFiles(moduleDir)`. No path component is constructed from
caller input — pure enumeration.

### 2. Symlink/junction escape — **not exploitable**

`Directory.EnumerateDirectories`/`EnumerateFiles` will follow symlinks
on .NET. `modulesRoot` resolves to `{App.OsDirectory}/system/modules` —
a repo-tracked directory whose contents are the same trust level as
the C# source tree. Planting a malicious symlink there requires write
access to the repo, at which point an attacker would simply edit C#
directly. No new boundary.

### 3. Unbounded read — **not a sink we score**

`File.ReadAllText` has no size guard, but the content flows only into
the LLM compile prompt. Per the review exclusions ("Including
user-controlled content in AI system prompts is not a vulnerability"),
and given the content is not user-controlled to begin with (repo
files), there is no scored vulnerability. DOS-shaped concerns are
excluded by rule.

### 4. Orphan-scan side channel — **not a vulnerability**

`WarnOrphansAsync` writes one line per orphan to the actor `Output`
channel:

```
Orphan teaching markdown: {path} (no registered action '{module}.{stem}'). …
```

Destination is the developer's terminal at build time. The path
revealed is inside `os/system/modules/` — the same tree the developer
just typed `plang build` against. No filesystem-layout disclosure
beyond what the developer already has read access to.

### 5. Attribute deletion regressions — **clean (spot-checked)**

Sampled the deleted attribute content on the higher-risk modules:

- `output/ask.cs`, `output/write.cs` — only `[Description]` strings;
  no security-bearing logic.
- `signing/sign.cs`, `signing/verify.cs` — `[Description]` only; the
  destination-pinning carry-forward F1 from `path-polymorphism` lives
  in `actor/permission/this.cs` and is untouched on this branch.
- `settings/get.cs`, `settings/set.cs`, `settings/remove.cs` —
  `[Description]` only.
- `mock/action.cs` — `[Description]` only.

In every case the *only* deletion is documentation-prose attributes
whose content has been migrated to `.description.md` / `.examples.md`
under `os/system/modules/<module>/`. No deletion changed a runtime
behavior, attribute-driven gating, or serialized error surface.

### 6. `MarkdownTeachingRoot` setter — **not reachable from a program**

The setter is `public string?` on `app.modules.@this`. The catalog
instance is owned by the engine and not surfaced as an action target.
No `[Code]`-attributed handler exposes the catalog as a parameter
type, no PLang action writes the catalog's properties, and the type
is not declared `IDataWrappable` for variable plumbing. Reachable only
from C# (host-process trusted code).

---

## Excluded by ruleset (noted for completeness)

- LLM prompt content fidelity / prompt-injection via repo markdown —
  rule 14 ("Including user-controlled content in AI system prompts is
  not a vulnerability"); content is also developer-authored on this
  branch.
- Unbounded `File.ReadAllText` — DOS/resource-exhaustion class, rules
  1/4.
- Documentation-only changes under `os/system/modules/*.md` and
  `Documentation/**` — rule 16.

## Carry-forwards from prior branches

`filesystem-permission` F1/F2/F4 and `path-polymorphism` F1/F2/F4
remain open on `main` (auditor PASS on `path-polymorphism` noted them
as expected). They are unchanged on this branch — no production file
under `actor/permission/` or `types/path/permission/` was touched.

No re-verification needed: the relevant files appear nowhere in
`git diff --stat d8c18e257..HEAD -- 'PLang/**/*.cs'`.

---

## Bottom line

Per-action LLM teaching files are repo-tracked developer documentation
that happens to be read at build time instead of baked into the
assembly. The new code surface is a read-only loader scoped to a
fixed directory, called only with reflection-derived identifiers. No
new attack surface; no findings.

**Ship.**
