# Retro v6 Findings — Coder sessions Jun 21-23 (compare-redesign era)

Sessions scanned: 5 (1f86445f, 5af76d61, 7704f9f0, 6b1d7425, d760f3f1)
Date range: Jun 21-23 2026

---

## Category: Frustration

### F1 — Hack-first instead of root-cause-first (3 sessions, highest recurrence)

**Evidence:**
> "why the fuck is there still type=this, I told you it should be type=list<dict>, dont just hack through fixing this, I dont trust you" *(1f86445f)*

**Pattern:** Bot sees a symptom, applies a patch, gets another symptom, patches that, never traces WHY. In 7704f9f0 the same: hand-fixing `.pr` files one-by-one instead of understanding why the builder produced wrong output. In d760f3f1: repeated rebuilds without new diagnostic information.

**Doc target:** MEMORY.md inline rule (strengthen existing `feedback_root_cause_not_obvious_place.md` reference — current link is too quiet)

---

### F2 — Verb+Noun violations every session (5 sessions)

**Evidence:**
> "plus, I think this is wrong approach, ClrValue should be the clue (Verb+noun, rule)" *(d760f3f1)*
> "no on BornRow, [verb+noun] obp violation" *(6b1d7425)*
> "I am sad of short you have come" *(6b1d7425)*

**Pattern:** BornRow, ValueClr, NewEmpty, LoadFromFileAsync, ConvertToIdentity, ParseNextSegment — every session has at least one. The Verb+Noun flashing sign IS in MEMORY.md prominently but it's written as a write-time tripwire ("when a name appears"). Coder keeps proposing them in plans before writing — the tripwire doesn't fire at proposal time.

**Fix needed:** The flashing sign must fire at **design/proposal time** too, not just write time. Add: "Before writing any plan/proposal, scan ALL intended names."

---

### F3 — C# edits before design discussion (2 sessions)

**Evidence:**
> "dont change c# lets talk about this before you do that. why is value now some special thing here??" *(1f86445f)*
> "let's do redesign this bothers me, write it up" *(7704f9f0, after bot proposed verb+noun design)*

**Pattern:** For significant API/architecture questions, coder implements first, discusses after. Ingi has to stop them.

**Doc target:** MEMORY.md — for any design change that touches type shape or API boundary, verbal plan FIRST.

---

### F4 — Rebuild loop without adding diagnostics (d760f3f1)

**Evidence:**
> "you are building too often, ... if you are building again, why dont you add debug message before building again??" *(d760f3f1)*

**Pattern:** Rebuild-test-rebuild cycle without adding logging. Each rebuild gets the same failure. Nothing is learned.

**Doc target:** MEMORY.md — each rebuild in a debug loop must earn new information (add diagnostic first).

---

### F5 — Diagnosing at the wrong layer (d760f3f1)

**Evidence:**
> "stop looking at c# code, fix the pr file. value should be last" *(d760f3f1)*

**Pattern:** Issue is structural (PR file field ordering) but coder dives into C# handlers. The correct fix is at a different layer.

**Doc target:** MEMORY.md — diagnose and fix at the layer where the problem actually lives.

---

### F6 — Text explanations instead of code (d760f3f1)

**Evidence:**
> "I need code, I need flow, text says nothing to me, didn't read what you wrote, to much" *(d760f3f1)*

**Pattern:** Coder provides paragraph explanations. Ingi wants code traces and flow.

**Doc target:** MEMORY.md Communication section — lead with code when explaining, not prose.

---

### F7 — Operations placed in wrong type (2 sessions)

**Evidence:**
> "Now.Ticks should be a datetime item, it should know how to navigate it self, I dont see what item.cs has to do with it" *(d760f3f1)*
> "shouldnt it be Number.FromText? and the number class knows how to convert it self, like obp says" *(d760f3f1)*
> "prPath reading should give back a goal I believe... if you are parsing it in goal/list/this.cs, that is wrong" *(d760f3f1)*

**Pattern:** Navigation/conversion logic placed in generic utilities (item.cs, goal/list) instead of on the owning type. OBP violation that keeps recurring even though the rule is stated in CLAUDE.md. Needs a concrete "ask yourself" check in MEMORY.md.

---

### F8 — Over-engineering / building abstractions that shouldn't exist (2 sessions)

**Evidence:**
> "why do you need ICreate<LlmMessage>?" / "because LlmMessage is just regular class, we dont want every regular class have to implement ICreate" *(6b1d7425)*
> "TryConvert does way too much, should not do any of that and should be deleted, there is no need for it." *(d760f3f1)*
> "your LoadFromFileAsync is some nonsense, path reads it, it knows how to read it, wtf is ConvertToIdentity??" *(d760f3f1)*

**Pattern:** Coder builds utility methods and interfaces that duplicate/wrap type-owned operations. When the design is right, these disappear. Self-correction in 6b1d7425: "I massively over-built it."

---

## Category: Self-Correction

### SC-a — Design split that collapsed (5af76d61)

> "You're right — I drew a false line. There aren't two axes; there's one IReader abstraction with multiple format impls"

**Lesson:** Architectural splits based on current usage (not domain shape) collapse when challenged. Check OBP before proposing splits.

---

### SC-b — Test fixture using Peek instead of await Value() (d760f3f1)

> "`DataWrappedStringUses.Run()` reads `Body.Peek()` (unresolved) instead of `await Body.Value()`"

**Lesson:** Test fixtures must use `await .Value()` on lazy parameters, not `.Peek()` on unresolved raw. Already in testing memory — CONFIRMED.

---

### SC-c — Hand-editing .pr files when only builder goals can be edited (7704f9f0)

> "wait, only files you are allowed to hand edit is the plang builder, if you were hand editing something else, let's just run the builder on them."

**Lesson:** `.pr` files are build output — only the builder can correct them. `feedback_plang_build_file_scoping.md` covers scope; but the "only builder goals are editable by hand" rule needs to be explicit.

---

## Category: Wrong-Doc

### WD-a — Coincidental duplication vs shared logic (6b1d7425)

Bot argued that two "yield seams" shared logic justified extraction. Ingi challenged. When the design was fixed, the duplication vanished — it was coincidental.

**Lesson worth writing:** Before extracting shared logic, ask "does this duplication disappear if the design is right?" If yes, it's coincidental — don't extract, fix the design.

---

## What's Already Covered (skip re-proposing)

- SC1-SC11: All applied in v1-v3
- Verb+Noun rule: IN MEMORY.md at the top (but needs strengthening — fire at proposal time too)
- Fix-test-not-runtime: SC10, line 50
- Debugging discipline: `debugging_discipline.md` linked at line 56
- Build scoping: `feedback_plang_build_file_scoping.md` at line 84

---

## New Rules to Apply

| ID | Rule | Target | Strength |
|----|------|---------|----------|
| SC12 | Root-cause mandatory before any code change | MEMORY.md inline (strengthen existing) | High — 3 sessions |
| SC13 | Verb+Noun fires at proposal/design time, not just write time | MEMORY.md flashing sign (amend text) | High — 5 sessions |
| SC14 | Significant design changes: verbal plan first, then wait for OK | MEMORY.md inline | High — 2 sessions |
| SC15 | Rebuild = must add diagnostic first (earn new information) | MEMORY.md Small Rules | Medium |
| SC16 | Fix at the correct layer (symptom's layer = fix's layer) | MEMORY.md Small Rules | Medium |
| SC17 | Lead with code in explanations, not prose | MEMORY.md Communication section | Medium |
| SC18 | Operations belong on the owning type — ask "which type owns this?" | MEMORY.md Coder discipline | Medium |
| SC19 | Coincidental duplication disappears with correct design — don't extract it | MEMORY.md Small Rules | Low |
| SC20 | .pr files = build output only; only builder goals are hand-editable | MEMORY.md Small Rules (strengthen existing) | Medium |
