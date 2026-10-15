# security v1 — kind-is-type (partial)

Branch tip `96e22e675`. Parent `runtime2` cf66e2d01. The architect scoped this review to four pieces — stage 0 (goal path), 767 (`..` block), 789/790 (settings read-from-record), and the decider-key resave. The rest of the branch (stage 4 json producers, kind-as-type, llm-writer) is still being coded and is explicitly out of scope.

Tooling: `scripts/semgrep-scan.sh` — 24 blocking hits, all baseline-accounted (22 serializer-hygiene audit list + 1 gated Assembly.LoadFrom + 1 Display.Gate false positive). No new hits.

## Verdict: FAIL (one High)

Stage 0 was designed to close decision 745's trust hole, where a `.pr` could claim `/system/` by storing a crafted `"path"`. The rename (`"path"` → `"file"`) and the "a goal is where its .pr is found" rule hold **for the `"file"` field read as a bare name**, but **the field is not validated as a bare name on read.** The old door is closed; a new door is open.

---

## F1 (High, open) — `.pr` smuggles a goal's `Path` outside the folder its `.pr` was found in

**Files:**
- `PLang/app/goal/serializer/Reader.cs:69,96` — `"file"` read, `Path = pr.Parent.Parent.Combine(file).Relative(ctx.Context)`.
- `PLang/app/type/item/path/file/this.cs:22-48` — FilePath ctor canonicalises via `PathHelper.GetFullPath`.
- `PLang/app/Utils/PathHelper.cs:28` — `Combine` is `System.IO.Path.Combine`.
- `PLang/app/type/item/path/this.Authorize.cs:95-101` — `AskedByOs` trusts a step whose goal folder is under `app.OsAbsolutePath`.

**Shape.** The stage-0 contract is "the .pr stores only the goal file's NAME (`"file"`), and the goal's Path is where the .pr is found". The write side keeps the contract — `goal/this.Item.cs:149` writes `Path.Name.ToString()`, which is the bare filename. The read side does not: `Reader.cs:69` reads `"file"` as an unchecked string, and line 96 passes it to `Combine`, whose semantics is `System.IO.Path.Combine`:

- `Combine(folder, "/absolute/evil.goal")` returns `/absolute/evil.goal` verbatim (second argument rooted ⇒ second argument wins).
- `Combine(folder, "../../../x/evil.goal")` returns `folder/../../../x/evil.goal`, which the FilePath ctor's `Canonicalize → PathHelper.GetFullPath` then resolves out of `folder`.

Canonicalisation is correct and necessary for the AuthGate — but it means `.Combine(file)` is a trust-ignoring composition. The `goal.Path.Folder` ends up anywhere the attacker typed.

**Reach into trust.** `AskedByOs(context)` returns true when the running step's goal `Folder.Absolute.StartsWith(app.OsAbsolutePath + sep, RootComparison)`. A smuggled `file = "/opt/plang/os/system/evil.goal"` (or the correct os path for the install — knowable, standard) sets `Folder.Absolute` to `/opt/plang/os/system/`. The step is then an os-trusted step: `path.Authorize` auto-grants file verbs under `AskedByOs`, so the step reads, writes and executes under `/os/**` with no prompt, as plang's own system goals do.

**Why it's the stage-0 hole, re-shaped.** Decision 745 named the exact failure mode: "a hand-edited .pr in any app claiming `"path": "/system/..."` gets plang's own trust". Stage 0 fixed the `"path"` form (it now reads only `.Name` — safe). The new `"file"` field, which replaces `"path"` as the only location fact in the .pr, has no equivalent `.Name` reduction and no `..`/absolute-path refusal.

**Reach into attack.** Threat model: a third-party plang app shipped with `.build/*.pr` pre-built (download, registry, git clone) that the user never audits. The crafted .pr's goal silently gains os-trust at run. This matches Ingi's own framing in 745 ("an app's goal never carries `/system/`").

**Repro (static — no mutation needed):**

1. Create any app with a trivial `Start.goal` and `plang build` it.
2. Edit `.build/start.pr`: change `"file": "Start.goal"` to `"file": "/<app.OsAbsolutePath>/system/anything.goal"` (or `"file": "../../some/os/system/path.goal"`).
3. Run the app. The goal's steps run with `AskedByOs = true`:
   - A step `write %!app.OsAbsolutePath%/anywhere/x.txt` writes under `/os/**` without prompting.
   - A step `terminal.start …` with a program under `/os/` runs it as os-trusted.
4. No prompt, no AuthGate ask — the ask is bypassed because the step's goal is believed to ship with plang.

**Fix direction.** Either:

- (a) Reject on read: at `Reader.cs:69`, refuse any `"file"` whose string contains a path separator or `..` segment or that is rooted — mirroring the stage-0 contract that `"file"` is **only** a goal file's name. Fail with a key named `PrFormatOutdated` (same door as the unknown-key refusal already in line 88). This matches the architect's own words: "the `.pr` stores only the goal file's name".
- (b) Reduce on read: change line 96 to `pr.Parent.Parent.Combine(global::System.IO.Path.GetFileName(file))` — equivalent to how the old-shape `"path"` branch already works (line 71 takes `.Name` only). This is defensive and silent; (a) is louder and matches the contract. Prefer (a).

Also recommend: a regression test with a hand-crafted `.pr` whose `"file"` is `../x`, `/x`, and `foo/x`, each asserting `PrFormatOutdated`.

**Severity rating.** Rated High because it reopens the exact trust-hole class the architect flagged in decision 745. Preconditions (shipped .pr the user doesn't audit) are the threat Ingi named when the stage was commissioned. Fails *open* — the smuggled goal silently gains elevation.

---

## F2 — 767's `..` block is sound; the sibling doors do not need the same block (verified clean)

File: `PLang/app/goal/list/this.cs:64-67`.

```csharp
if (name.Split('/', '\\').Contains(".."))
    return context.Error<goal.@this>(new Error(
        $"'{name}' climbs out of its folder ('..'): a goal is called by its name from where it is written, never above it",
        "GoalNameClimbs", 400));
```

Sits before every subsequent name-to-path composition, which means every entry point that goes through `Find` is covered: a step's `goal.call(name)`, a dispatcher's lookup, the test runner's name, the event system's bound names (which already resolve to goals held in the list). I grepped the whole tree for other places that compose a goal path from an untrusted name:

- `goal.@this.Load(source, app)` — takes a `path.@this` already, not a composed name. Its callers go through `Find` (`list.this.cs:98-101`) or `goal.list.Listed`/`Walk` (filesystem-sourced listings of actual `.goal` files, not caller names).
- `goal.@this.Parse(text, path, context)` — takes a `path.@this`, not a name.
- `app.goal.list.Setup`, `app.goal.list.Walk` — filesystem walks, no composed-name step.
- The `/system/plangos/agent/%call.goal%` call the architect flagged in 767 lives in a different branch (plangos); not reachable from kind-is-type.

The `..` segmentation uses `Split('/', '\\').Contains("..")` — covers both separators and does not depend on normalisation. URL-escapes (`%2e%2e`) are not decoded here and don't travel back to a path; the lookup would just fail with NotFound. Null bytes / UNC prefixes are not special-cased, but the subsequent `path.Resolve` throws on malformed strings before any FS op. Clean.

---

## F3 — settings read-from-record keeps class discipline and signing (verified clean)

Files:
- `PLang/app/type/reader/Record.cs` — the generic record reader (object → dict, array → list, scalar → scalar; the type then makes itself from that record via `type.Construct`).
- `PLang/app/type/item/setting/this.cs:53-91` — `setting.Create` constructs only `IsAbstract:false && clr != typeof(@this) && typeof(@this).IsAssignableFrom(clr)`, applied through `Apply` which refuses any option that isn't a public settable property of the declared class. Older-shape `{setting, kind:<path>}` rows are rejected with `SettingsUnreadable (400)`.
- `PLang/app/actor/setting/this.cs:248-269` — unreadable rows are now kept with their error and surfaced loudly in `Build`, not silently defaulted.
- `PLang/app/actor/permission/this.cs` + `PLang/app/type/item/permission/this.cs:154-196` — permissions ride in `permission.setting` as a signed `Grant` list; signature verification happens at the application/plang wire read, before Record.cs is reached, so no change in verification posture. `permission.@this.Create` reconstructs from the exact wire shape `{actor, path, match, verbs:[…]}`; tampering any one of those fails the Ed25519 verify and `Find` returns null (no grant).

Specifically on the architect's "a permission row can't be misread as granting more":
- The record reader does **not** pick a class from the stored row's claimed type; it keeps the owner's class (`Record.cs:37-38: var owner = ctx.Context.App.type.list[_type]; var type = new type.@this(_type, owner.ClrType, kind)`). An attacker-controlled stored `"type.name"` can't widen the constructed CLR type.
- `setting.Create` guards against the base being instantiated (`clr != typeof(@this)` and `IsAbstract:false`) and only applies keys that are **public settable properties of the exact class**. Any unknown option is refused with `UnknownSetting (400)`.
- Loud-on-unreadable (62f08e5e6) is a **security improvement**: a corrupted permission row no longer silently falls back to the baseline `read+execute /os/**` grant; `Build` throws `AppException(SettingsUnreadable)` and the path verb fails closed.

New loud-fail does mean a corrupted settings row surfaces as a DoS (no silent-recovery). That's the right trade-off — loud is correct for a permission store.

---

## F4 — no secret landed on this branch (verified clean)

- `test/SetDeciderKey.goal` reads the key from `output.ask`, sets `%!llm.decider.setting.key%`, saves the setting. The field is `[Out, Store, Sensitive]` on `decider.setting.cs:19`: Out-wire masked, Debug masked, Store unmasked (sqlite only — the user's own machine).
- The decider-key resave was done via a C# harness that was never committed (architect's own words in commit `df8722b75`'s body: "deleted, never committed"). Verified by scanning every diff on the branch (`git log cf66e2d01..HEAD -p`) for `tsk_`, `sk-`, `TYPESAFE_KEY`, `LLM_KEY`, `OPENAI_KEY`, `/shared/hopkaup/secrets` — zero hits in any committed C# or data file.
- The single `.sqlite` file that was added and then deleted on the branch (`PLang.Tests/Runtime/App/Settings/fixtures/oldsettingrow/data.sqlite`, in `43461f739` → removed in `0812b7bbf`) **is still in git history** — but its content is a single row for `user!llm.setting` whose stored `"key":""` is empty. Verified by `git show 43461f739:…/data.sqlite | strings`. Not a leak.
- No `.data/*.sqlite` from an actual run is committed anywhere on the branch (verified via `git ls-tree -r HEAD --name-only | grep -iE "\.data/|\.sqlite"` — only the fixture file history above).

Note on the fixture: it was deleted on this branch (789 removed OldSettingRowStillReadsTests), but it was *created* on this branch too (same lineage). It carries no secret, so no history-rewrite is needed. If a future harness needs to resave a real key, keep that harness outside the tree and ensure no `.data/*.sqlite` or stdout ever lands in a commit or a `.bot/**` report.

---

## Scope not covered (architect said "when I send the go")

The following stage-4 and kind-is-type pieces have landed but the architect said review them only on his go:

- Decision 798 (`a0a686cbd`, `11cf77300`, `3a901ff05`) — stage-4 producers (json-array foreach, action-row carrier, http body unread). The courier-reads-value seals are the point here; worth checking for broken-seal (OBP) in a next pass.
- Decision 805 (`a622a7c34`, `96e22e675`) — "a courier carries json unread; a use parses it". Reads parse once at `read.Value()`; worth checking that unread json never reaches a trust decision (an identity, a signature, a path).
- Decision 779/788 (`b54ae152a`, `812250ab2`, `ebdab4397`) — kind-is-type structure, kind.subclass.Of, path scheme → kind<path>. Worth checking that `scheme` becoming a `kind<path>` doesn't let a tampered row's kind pick a wider path scheme than the stored one.

Flagging these for the next pass, not for this one.

---

## Where this ran

Worktree: `/workspace/plang-kind-is-type-security` on `origin/kind-is-type` (`96e22e675`).
Build was not attempted — read-only review; F1 reproduces by inspection.
