# Stage 2: Storage Binding

**Goal:** Wire `Permission/@this` to the app's system variables so `List()`, `Add(Data<Permission>)`, and `Check(Path, Verb.@this)` operate against real persisted state.

**Scope:** Bind `@this` to the variables system. Reading and writing the signed-Data list. Process-only vs. persisted grants. Lazy/eager load decision.

**Excluded:** The signing itself (PLang plumbing). The prompt UI (PLang plumbing). The FS surface rewrite (stage 3).

**Deliverables:**

- `Permission/@this` filled in: constructor takes whatever binding it needs to reach `app.System` variables, `List()` reads, `Add(Data<Permission> signed, bool persist)` writes, `Check(Path, Verb.@this)` composes.
- Variable name pinned (likely `filesystem.permission`, but confirm by reading existing variables and actor source first — see `plan/open-questions.md` #2).
- Process-only grants kept in an in-memory list separate from the persisted variable (decision in `plan/open-questions.md` #5 — leaning keep distinct).
- C# tests for `Add` → `List` round-trip, `Check` against a populated grant store, process-only vs. persisted divergence.

**Dependencies:** Stage 1 complete. Permission types must exist before they can be stored.

## Design

The storage approach is in [plan/storage.md](v1/plan/storage.md). Two things the coder must settle first:

1. **Read `PLang/App/Variables/` and the system-actor wiring** to understand the existing variable read/write API. Match the convention — don't invent new patterns. Whatever existing types persist via, Permission persists the same way.

2. **Decide if grant verification (signature check) happens at load or per-access.** Strong preference: variables system handles it once at load, Permission trusts what it unwraps. If the variables system doesn't do verification today, escalate — this is a security-critical question outside the architect's seat alone.

## What stage 2 does NOT do

- Doesn't define how the user's prompt response becomes a `Data<Permission>`. That's PLang's "ask user, permission:high" plumbing — out of scope here.
- Doesn't touch `IPLangFileSystem`. Permission/@this exists in isolation; nothing routes through it yet.

## Acceptance

Permission/@this can be constructed for any app, populated with signed grants programmatically (skipping the prompt), and answers `Check(...)` correctly. Tests verify round-trip persistence — restart the actor, grants survive.
