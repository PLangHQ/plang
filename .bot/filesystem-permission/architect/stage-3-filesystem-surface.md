# Stage 3: Filesystem Surface Rewrite

**Goal:** Drop `System.IO.Abstractions.IFileSystem` inheritance. Rewrite `IPLangFileSystem` to a Path-shaped, `Data<T>`-returning surface. Route every operation through `Permission/@this.Check` at the boundary. Default code does the local-disk work; nothing else changes.

**Scope:** The interface, the Default code, all consumer call sites in actions and runtime internals.

**Excluded:** The error type (`PermissionRequired` — stage 4 defines its fields). The Messages app end-to-end (stage 5).

**Deliverables:**

- Three survey passes (read [plan/filesystem-surface.md](v1/plan/filesystem-surface.md) for the methodology):
  - **Pass 1**: every action under `PLang/App/modules/file/` and other actions that touch the FS.
  - **Pass 2**: every runtime/internal call site (`PLang/Runtime2`, `App/Goals`, `App/Snapshot`, `App/Settings`).
  - **Pass 3**: Directory operations across both.
- Closed-list inventory of operations as a doc table — name, current BCL shape, proposed Path-shaped shape, return type, whether `Verb.@this requested` is an explicit parameter.
- `IPLangFileSystem` v2 defined per the inventory. No `IFileSystem` inheritance.
- Default code at `PLang/App/FileSystem/Default/` (renamed from current `Default/PLangFileSystem.cs` shape) implements v2. Internally still uses System.IO.Abstractions; that stays an implementation detail.
- Every call site migrated. Old `IFileSystem`-shaped calls replaced.
- C# tests for the Default code per operation (read, write, delete, exists, list, mkdir, move, copy) — round-trip with permission Check stubbed to always-pass and always-fail.

**Dependencies:** Stages 1 and 2 complete. The Permission Check call has to be ready before we can route through it.

## Design

The surface rewrite is in [plan/filesystem-surface.md](v1/plan/filesystem-surface.md). Two things this stage owns end-to-end:

1. **Closed list discipline.** Don't migrate methods that aren't used. The point of dropping `IFileSystem` is to *shrink* the surface, not preserve everything BCL gave us for free. If a method shows up in the survey only in dead code, delete the call sites in the same stage.

2. **Verb explicit vs implicit per method.** From `open-questions.md` #1, partially: the FS surface decides for each method whether the caller passes `Verb.@this` or whether the FS layer constructs the default. Inventory recommendation: explicit for operations whose verb meaningfully varies (List wants `Read{Recursive}`), implicit for trivially-typed operations (Exists is always `Read{Metadata=true}`).

## Pre-stage conversation gates

Before stage 3 starts, the architect and Ingi should settle:

- **App-side cascade** for requested verbs. Does an app declare its intent globally / per-goal, or does each action pass the verb explicitly each time? Affects whether some methods take `Verb.@this` at all.
- **Content sub-option on Read.** If we add `Content: bool` to Read, stage 1's types need updating before stage 3 lands. Decide before this stage starts; doing it after means rework.

## What stage 3 does NOT do

- Doesn't define `PermissionRequired`. Stage 3 uses a placeholder error so call sites compile; stage 4 swaps in the real type.
- Doesn't touch the Goal-mapped FS code (parked).
- Doesn't add the consent prompt — stage 4 wires escalation.

## Acceptance

`dotnet build` clean. All existing tests pass (modulo expected changes where call sites moved from string to Path). New per-operation tests pass. The `IFileSystem` import is gone from `IPLangFileSystem.cs`.
