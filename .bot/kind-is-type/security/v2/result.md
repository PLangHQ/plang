# security v2 — kind-is-type

Branch tip `356d9ca44`. Parent `runtime2@cf66e2d01`. V1 covered stage 0 / 767 / 789-790 / decider-key resave; this pass covers everything on the branch since v1 that the host called out — stage 4 (json's uses-parse model), decision 814 (`Internal` on item's host kinds), file.read's `Type` slot and its Build, stage 5 (the builder's pick/list kind follow-up), stage 6 (`.kind` direct, F4.2 face, F8.1 Takes door), the render fix at Fluid.cs:160, and the re-check of v1's F1.

Tooling: `scripts/semgrep-scan.sh` — 24 blocking hits, all baseline (22 serializer-hygiene audit list + 1 gated Assembly.LoadFrom + 1 Display.Gate false positive). No new.

## Verdict: PASS

No merge-blocking findings and nothing new to carry. The deltas since v1 are mostly type-system plumbing (locked-core-adjacent) and build-time teaching — they do not touch AuthGate, signing, channels, the terminal sandbox, Fluid's SSTI sandbox, or settings isolation. The one trust-adjacent change (file.read's `Type` slot) still gates reads through `path.Bytes` before the declared type's birth; the render-fix is a landed-value type change, not a sandbox change.

---

## F1 re-check — still closed (`goal/serializer/Reader.cs:69-80`)

The predicate is unchanged since v1 recheck (`empty || "." || ".." || IndexOfAny('/','\\',':')`), landed at `d9bc6aae7` and verified in `.bot/kind-is-type/security/v1/recheck.md`. The pins at `GoalPathTests.APrFile_ThatNamesAPath_IsRefused` are intact (`../evil`, `/evil`, `sub/evil`, `..\evil`). The one `.pr` location-fact compose site remains the single gate.

## Stage 4 — json rests, uses parse (decisions 814, 822, 824–830)

Threat question: an untrusted `{item, json}` value (the result of http.get, file.read, a channel receive, a goal.call parameter) now rides unopened in a courier until a consumer touches it. Does the parse-when-touched model open any new DoS vector, a new place where unread bytes reach a trust decision, or a late parse that bypasses an earlier check?

Read `PLang/app/type/item/kind/json/this.cs`:

- `Load` (line 36-41) returns an `{item, json}` source holding the raw bytes without parsing — the "rest" state.
- `Parse` (line 73-95) opens a raw into a plang value through the token readers. `new Utf8JsonReader(bytes)` (line 82, 114) uses default `JsonReaderOptions`: `MaxDepth = 64`, `AllowTrailingCommas = false`, `CommentHandling = Disallow`. The default MaxDepth matches `data/Wire.cs:78 MaxReadDepth = 64`, so an untrusted payload cannot escape the depth cap by riding the json kind path.
- `Open` (line 99-103) catches `JsonException` → `MaterializeFailed, 400`.
- `Read` (line 109-121) catches `JsonException` → `Unread`.

Verified clean:

- No new unbounded recursion on parse-at-use (depth cap at Utf8JsonReader default).
- Unread bytes do not reach a trust decision — the signing pipeline operates on the wire shape BEFORE kind-level decoding; the signature covers the whole value including the json bytes, so tampering fails verify before Load is called. A verified `{item, json}` source is trusted bytes-wise, and the parse-at-use just gives it structure. No new signing escape.
- A consumer that uses a typed ask (`Value<dict>()`, `Value<text>()`) materializes through the type's own birth; the generic record reader does not widen the CLR type (verified in v1 against Record.cs:37 — still unchanged).
- The courier-reads-value sealing (OBP broken-seal rule) is enforced by the `.Value` typed/untyped split at the `data.@this.Value()` layer — carriers that just forward don't crack the value.

## File.read's Type slot (`PLang/app/module/file/read.cs`)

The step's `as <type>` becomes a `Type` slot (`data<type>?`); `Start()` reads bytes through `path.Bytes(Context)` and births the declared type from them. Trust posture:

- Line 32-43: `Path.Use(async path => ...)` opens the FilePath; `path.Bytes(Context)` is `FilePath.Bytes`, which runs `AuthGate(read)` before any byte reaches the caller. Unchanged.
- Line 38: `declared.Create(bytes, Context, name, path)` — the declared type's own birth. The declared type comes from the step's words, not from the file's content, so content-driven type confusion is not possible. The declared type's `Create` can only reach I/O through its own gated paths (and does not generally touch the FS at all for the common types text/json/dict/list — this is a value-level birth).
- `Build()` at line 50-63 returns the Type carrier itself when `Typed()` (no I/O), or `path.Expect(Context)` for a literal path (gated inspection, not read). A variable path with no Type returns `Context.Ok()` (nothing published, as it should at build).

Verified clean. The `as <type>` force does not weaken AuthGate: a path still passes `read` before bytes come back, and the type that births is the step's own force, not the file's content.

## Stage 5 (builder's pick/list kind follow-up, decisions 816, 817, 835, 839, 840)

Per decision 835 "no runtime C#" — these are builder teaching (`os/system/builder/llm/templates/decider3.template`, `.notes.md` files, type descriptions) and re-pins against the decider's prompts. The one C# change in stage-5 territory is the move to a `KindOf` question in `Decide.goal` + a conditional third exchange — those are plang goal + LLM prompt text, not runtime C#.

Verified clean: build-time teaching cannot change runtime trust. The decider still runs behind the builder's authentication to its decider provider (`llm.decider.setting.key`); the builder operates on `.pr` output which flows through the normal reader at run (and that reader is now gated by F1's bare-name refusal, so the stage-5 teaching cannot smuggle a path through the `"file"` field even if a template line went wrong).

## Stage 6 (decisions 833, 834)

Three changes landed:

- `.kind` is answered directly (`type.@this`); the `80e4c9e4e` stopgap gone; dead `TypeDescription` / `subclass.Offers` reflection gone.
- F4.2: a kinded type's face writes `name<kind>` (`type.ToString`): a pure display/encoding change on `type.@this`.
- F8.1: slot acceptance asks the slot's type through `Takes`, not an internal `TakesAction` branch — a cleanup, not a widening.

Decision 834's #3 (a source born with the full type) regressed ~13 tests and was **reverted**. The branch tip does not carry it, so no review needed on a reverted-and-left change. Both the F4.2 face and the F8.1 `Takes` cleanup are code-shape improvements; neither reaches AuthGate, signing, channels or settings.

Verified clean.

## Fluid.cs render fix (`2e83d21ff`, `module/ui/code/Fluid.cs:160-167`)

Change: a rendered template's output is a **plain text** by default; only when the format's parent is `item` (the structured kinds, so a `.json` template rests as `{item, json}`) does the output land through `format.kind.Decode`. Previously every format decoded, so an `.html` template landed as a code-kinded text that would not compare equal to a plain text.

Security questions and answers:

- **Does the output-type change affect SSTI?** No. Fluid's rendering itself is unchanged (line 148-154); the HTML encoder discipline (line 147), the `PlangFileProvider` include-sandbox (`includes.Load` at line 122 + `GetTemplateBaseDir`), and the `callGoal` tag's own `ValidatePath` are not touched by this diff.
- **Does the change allow a template to carry untrusted structure through?** No — the opposite: an unknown or non-item format now lands as a plain text, so the content is **less** structured, not more. A program that then navigates the result through `.Get(...)` on a plain text gets a NotFound, not a widened kind handle.
- **Does `{item, json}` still rest unopened?** Yes — same `format.kind.Decode` call that stage 4 relies on.

Codeanalyzer carries F3.v3 as a design-smell (symptom-patch via `is X? by name`); that is a code-quality concern routed to branch A, not a security concern.

Verified clean.

## Decision 814 (`Internal` on item's host kinds — `*`/reflection, dict, list)

Pure visibility flag telling the builder "don't offer these kinds to the user". It does not change what the type system permits at run; the kind machinery still answers members through the usual chain. No security surface.

Verified clean.

## Not in scope / not reviewed

- Decision 848 — a process complaint about `ObpScan` friction. Not security.
- Decision 850 — a locked-core proposal (`type.list[name]` returns a type with a bare kind) that the architect routed to branch A; llm-writer applies a local workaround for image/binary. Not landed in kind-is-type.
- Decisions 843, 844, 845, 846 — llm-writer branch's own work (service-actor plan, IWriter.Bytes door, pair serializer, llm kind). Those live on `llm-writer`, not here.
- The stage-4 producer sweep's non-security choreography (navigator deletion, text-wrapper retirement, Compare/Set/Meant migrations) — reviewed as a correctness refactor by codeanalyzer v2/v3; no security surface.

## Standing items from v1 that still hold

- 767's `..` block at `goal.list.Find` (unchanged).
- 789/790 settings read-from-record (unchanged).
- No secret in repo or branch history (last pass verified; no new committed `.sqlite` / `.pem` / `secrets/` on the branch since v1 — verified via `git log 9c1d252b7..HEAD --name-only | grep -iE "\\.sqlite|\\.pem|secrets"` — zero hits).

## Where this ran

Worktree `/workspace/plang-kind-is-type-security` on `origin/kind-is-type` (`356d9ca44`). Static review only; the architect's "11/11 pins" and the tester v2's "no new reds" statements are accepted on the record. Build was not attempted — none of the deltas since v1 need a run to decide the trust posture.
