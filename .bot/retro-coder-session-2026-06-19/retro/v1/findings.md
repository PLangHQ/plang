# Findings — Coder Session 2026-06-19

Session file: `/peer-sessions/coder/projects/-workspace-plang/5af76d61-0fd6-4ee1-a3bc-976d2c6d04f1.jsonl`

---

## SC1 — Relayed subagent claim without verifying

**Timestamp:** 2026-06-19T14:37:05Z  
**Category:** self-correction  
**Trigger:** After delegating test migration to subagents, coder reported that "explicit kind/strict declarations can't be expressed via Make.Param" — treating an agent's assertion as a verified architectural fact.  
**Ingi's correction:** *"why can that be? I mean, pr files have it, so we should be able to do it"*  
**Bot's admission:** *"You're right — that rationalization was wrong. `app.type.@this` owns kind+strict. Its ctor is `@this(string name, string? kind = null, bool strict = false)` — nothing about the read path loses them."*  
**Root cause:** No rule: verify subagent claims against actual source before asserting them.

**Applied to:**
- `coder/MEMORY.md` → *Coding Discipline*
- `coder/CLAUDE.md` → *OBP is the deliverable*

---

## SC2 — Added a method that already existed

**Timestamp:** 2026-06-19T15:19:15Z  
**Category:** self-correction  
**Trigger:** Coder added `PeekValue()` to variables — a wrapper around `Peek()` — without checking whether `Peek()` already existed.  
**Ingi's correction:** *"wow, what is PeekValue? we have Peek to get current value, or Value to get value"*  
**Bot's admission:** *"You're right — `PeekValue` is a redundant verb+noun method. `Peek()` is already the sync door on the Data; I shouldn't wrap it."*  
**Root cause:** No habit of inspecting existing type surface via LSP before adding a method.

**Applied to:**
- `coder/MEMORY.md` → *Coding Discipline*
- `coder/CLAUDE.md` → *OBP is the deliverable*

---

## SC3 — Kept production code alive only for tests

**Timestamp:** 2026-06-19T15:13:18Z  
**Category:** self-correction  
**Trigger:** Coder proposed leaving `GetValue` on the production `Variables` type because 103 test call sites used it, calling it "the test-facing door."  
**Ingi's correction:** *"so are you saying we are leaving code in runtime just for test?"*  
**Bot's admission:** *"You're right — that's a smell. Production code that exists only to serve tests is exactly backwards; tests should use the same doors production uses."*  
**Resolution:** `GetValue` moved to `PLang.Tests/Shared/VariablesTestExtensions.cs`.  
**Root cause:** No rule: if a method has only test callers, it belongs in test extensions, not runtime.

**Applied to:**
- `coder/MEMORY.md` → *Coding Discipline*
- `coder/CLAUDE.md` → *Testing Requirements*

---

## SC4 — Split an abstraction by current usage, not domain shape

**Timestamp:** 2026-06-18T21:41:04Z  
**Category:** self-correction  
**Trigger:** Early IReader design proposed splitting by format type based on "we use JSON for .pr" — treating current usage as a domain boundary.  
**Ingi's correction:** *"aren't we mixing pr parser, parsing plang type and regular json, and i don't see why same reader abstraction can't be used for binary?"*  
**Bot's admission:** *"You're right — I drew a false line. There aren't two axes; there's one `IReader` abstraction with multiple format impls, and I was confusing 'the .pr happens to be JSON' with 'the reader must be JSON.'"*  
**Root cause:** No OBP-framed rule for asking "why is this split here, does it follow OBP?" when a boundary appears.

**Applied to:**
- `coder/MEMORY.md` → *Coding Discipline*
- `coder/CLAUDE.md` → *OBP is the deliverable*

---

## SC5 — Feedback rule added to file, MEMORY.md pointer buried

**Timestamp:** 2026-06-19T18:31:39Z  
**Category:** self-correction  
**Trigger:** Coder added the verb+noun feedback rule to a separate file and put a pointer mid-list in MEMORY.md. Ingi had to point out that MEMORY.md is the loaded index.  
**Ingi's correction:** *"would it not be more permanent if it was in memory.md"*  
**Bot's realization:** *"MEMORY.md is the file loaded into context every session, so anything there is always in front of me."*  
**Root cause:** Coder didn't understand that individual memory files are not auto-loaded; only MEMORY.md is.

**Applied to:**
- `coder/MEMORY.md` → *Memory System*

---

## SC6 — Allocate-then-transform instead of own-at-birth

**Timestamp:** 2026-06-19T00:24:52Z  
**Category:** self-correction  
**Trigger:** Designed `text.Authored()` as a post-construction stamp, which is the create-then-transform OBP smell.  
**Ingi's correction:** *"yes, it's cleaner. `text.Authored()` is itself a tiny version of the create-then-transform smell. The reader knows its mode; it should hand the template to the type at construction."*  
**Bot's admission:** *"Yes — I think you're right, and it's cleaner."*  
**Root cause:** Didn't apply the allocate-here/transform-there smell check at design time.

**Applied to:**
- `coder/MEMORY.md` → *Coding Discipline*
