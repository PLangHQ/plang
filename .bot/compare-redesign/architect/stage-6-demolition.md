# Stage 6: Demolish the old machinery, rename the golden-diff

**Goal:** Delete the old comparison path now that every consumer is on `Compare`, and free the `Compare` name on the golden-diff so the value-comparison owns it.
**Scope:** Delete the static mediator, the scalar leaf, the coercion pass, the two interfaces, and the old per-type equality/order. Rename the golden-diff. Excludes any new behaviour — this is removal only; both suites must be green before and after.
**Deliverables:**
- Delete `PLang/app/data/Compare.cs` (the `app.data.Compare` static mediator + `NotOrderableException`).
- Delete `PLang/app/data/ScalarComparer.cs`.
- Delete `Operator.NormalizeTypes` (and its helpers `IsTextLike`/`IsNumberLike`) from `PLang/app/module/condition/Operator.cs`.
- Delete `PLang/app/data/IEquatableValue.cs` and `PLang/app/data/IOrderableValue.cs`, and remove those interfaces from the 12 type classes that implement them, plus their `AreEqual`/`Order` methods. (If you kept these alive through Stages 2–5 so the old mediator stayed green — see Stage 2's coexistence note — this is where they come out.)
- Decide `ITextCoercible`'s fate: its coercion role is now owned per-type inside `Order` (Stage 3). If nothing else uses it, delete it too; if something does, leave it and note why.
- Rename the golden-diff `data.Compare` → `Diff`: rename the method in `PLang/app/data/this.Compare.cs`, rename the file to `this.Diff.cs`, and update the ~14 call sites (all in `PLang.Tests/App/DataTests/DataCompareTests.cs`; no production callers).
**Dependencies:** Stage 5 (all consumers off the old path).

## Design

This stage is pure subtraction — its only risk is a missed reference. Work by deletion-and-compile: remove one piece, build, fix the fallout, repeat. The build is the proof; nothing here should add a branch or a behaviour.

Order that minimises churn:
1. Rename golden-diff `Compare` → `Diff` first (frees the name; touches only the diff method + its tests).
2. Remove the per-type `AreEqual`/`Order` and the `IEquatableValue`/`IOrderableValue` interface declarations from the 12 types.
3. Delete `IEquatableValue.cs` / `IOrderableValue.cs`.
4. Delete `ScalarComparer.cs` and `Compare.cs`.
5. Delete `NormalizeTypes` + helpers from `Operator.cs`.
6. Resolve `ITextCoercible` (delete or keep, with a one-line reason).

After each deletion the build names every site that still depends on the removed surface — that list is the work. When it's clean and both suites are green from a clean build, the redesign is complete: comparison is owned per type over a value that lives once in `Data`, reached through one lazy `ValueTask` door, with no static mediator, no `Type.Name` switch, and no second copy of the value anywhere.
