# Docs v1 Plan — runtime2-builder-v2-cleanup

## Context
This branch is a comprehensive cleanup of pieces 1-4 (identity, crypto, signing, http) plus engine-level changes. Coder did 2 versions, auditor approved (PASS), tester approved (PASS), security approved (PASS). 241 files changed, ~30K lines.

## What Needs Documentation

### 1. Architecture Docs Updates (Documentation/App/)

**modules.md** — Stale references:
- `library` action listed in Built-in Action Handlers table → should be `module` (with `add`, `remove`)
- `IdentityVariable` references → should be `IdentityData : Data`
- `IdentityData` section describes the old lazy wrapper → rewrite for new `IdentityData` (extends Data directly)
- Identity actions list: `export` now "Returns full IdentityData" not "raw private key string"
- `SignedData.Sign()`/`SignedData.VerifyAsync()` → pipeline moved to `ISigningProvider` (`Ed25519Provider`)
- Signing section references `SignedData.CreateAsync(sign action)` → now `ISigningProvider.SignAsync`/`VerifyAsync`
- `condition` section: remove `__condition__` signal reference — fixed in this branch (only condition module steps can skip children)

**good_to_know.md** — Updates needed:
- `IdentityData — Lazy Resolution` section: `IdentityData` is now `IdentityData : Data`, no longer a lazy wrapper. Update to reflect new pattern.
- `%MyIdentity% — DynamicData Registration`: references `IdentityVariable` → should be `IdentityData`
- `Libraries Replaces ActionRegistry`: mentions `engine.Modules` → verify naming is current
- `Sub-Step Execution — The __condition__ Signal`: update to reflect condition-only child skipping fix
- `Path Moved to Engine/FileSystem/`: class is now `PathData : Data`, not just `Path`

### 2. User-Facing Docs (docs/modules/)

**index.md** — Major updates:
- Remove `convert` module (deleted)
- Remove `archive` module (deleted)
- Rename `library` → `module` with new actions (`add`, `remove`)
- Add new modules: `crypto`, `http`, `identity`, `signing`, `provider`
- Update `event` actions list (6 separate actions → `on`, `remove`, `skipAction`)

**library.md** → Rename to **module.md**:
- Rename from library to module
- Add `remove` action
- Update examples to use `module` terminology

**event.md** — Major rewrite:
- 6 separate actions (beforeGoal, afterGoal, etc.) → single `on` action with `Type` parameter
- Update all examples

**New docs needed** (5 modules):
- **crypto.md** — hash, verify actions
- **http.md** — request, download, upload, configure actions
- **identity.md** — create, get, list, archive, unarchive, rename, setDefault, export
- **signing.md** — sign, verify actions
- **provider.md** — load, remove, list actions

**Deletions:**
- `archive.md` — already deleted in git, remove from index
- `convert.md` — already deleted in git, remove from index

### 3. XML Doc Comments

Focus on new public types missing docs:
- `modules/module/remove.cs` — class + members
- `modules/event/skipAction.cs` — class + members (has [Example] but no XML)
- `modules/event/on.cs` — class + members
- `modules/Attributes.cs` — ExampleAttribute, ProviderAttribute, IsInitiatedAttribute, IsNotNullAttribute
- `Engine/FileSystem/PathData.cs` — path properties (13 properties)
- `Engine/Memory/Data.cs` — DataList<T>.FromError, Data<T> class
- `Engine/Goals/Goal/GoalCall.cs` — Name, Parameters, PrPath properties

### 4. What I Won't Do
- Write PLang .goal examples (tester's job)
- Write code changes beyond XML comments
- Update builder prompt

## Order of Work
1. Write XML doc comments on new public types
2. Update `modules.md` (architecture)
3. Update `good_to_know.md` (architecture)
4. Rewrite `event.md` (user docs)
5. Rename `library.md` → write `module.md` (user docs)
6. Write new module docs: crypto, http, identity, signing, provider
7. Update `index.md` (user docs)
8. Write verdict + reports
