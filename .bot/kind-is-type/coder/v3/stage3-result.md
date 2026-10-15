# Stage 3 result — every family's kinds are types

Branch `kind-is-type`, decision 774/779/781. The one mechanism — a family's kinds are `kind<T>` — is **done and green** for every codec and format family. Several demolition/behaviour items are OPEN (blocked on Ingi, a locked-core decision, or live callers), detailed at the end.

## Done (committed, green)

**The mechanism.** `kind<T>` now provides `Owner` from T (`type/kind/this.Generic.cs`), so a family's kind base derives `kind<family>` and drops its string `Owner` override. Migrated:
- **Codec families → `kind<T>`:** number (stage 1; Owner override dropped), duration, size, hash, archive, conversation, junit, wire (`kind<wire>`, Q3). Each registers unchanged via `Every()`+Owner (Owner now from T).
- **`[Format]` families → `kind<T>` (Q1-A):** one concrete generic `app.type.kind.format.@this<T>`; the registry mints one per `[Format]` typed to the declaring type (`Registry.cs:316–323`). `[Format]` stays the declaration. text md/xml, image's 8, table csv are now `kind<T>`.
- **path schemes → `kind<path>`** (`path/scheme/this.cs`); registration in `app/this.cs` unchanged.
- **Subclass families (input/screen/browser/query):** `[Kinds]` + the `FamilyOf` base-walk STAY (their value classes stay `: parent.@this`, carrying abstract behaviour); the enlisted kind OBJECT is now the typed generic `kind.subclass.@this<T> : kind<T>` (`Registry.cs:359–365`). This is the architect's and the trace's shared conclusion — they can't become `kind<T>` themselves.
- **list element kinds → `kind<list>`** (`list/kind/this.cs`, `element/this.cs`).
- **bool, choice, separator** are the named-value families: choice sets stay as the closed-set mechanism; bool and separator are below.

**Gate.** C# full sweep: the 8 pre-existing baseline fails only (surface re-pinned under 774, no new C# reds; `TheNestedWrittenForm`/KindNests is a pre-existing stage-3 TODO — see OPEN). Plang suite **254/24/12/3 — identical to the stage-2 baseline, zero regressions** (the migration is a behaviour-preserving C# refactor). Surface golden green (re-pinned; diff is only the `kind<T>` bases + the new generic classes; **no type name changed**). Named tests: `SameKindNameUnderTwoTypes` (C# `AKindNameTwoTypesHold`) green; `ListKnowsItsElement` (plang) green.

Commits: f4a849b8b (codec) → ebdab4397 (path+bool, later reverted) → 6ebe182eb ([Format]) → 62bae8e0b (kind.subclass) → 5d6f5c97f (list element) → d1ab27ccf (bool revert) → 6530916be (surface re-pin).

## OPEN (need a ruling / blocked)

1. **bool needs its kind — `IsClosed`.** I removed bool's kind and moved true/false to a static `Offer`, but `PickOptionTests.AChoiceAndABoolAreClosed` pins `variable.set.Default` (a bool) as `IsClosed=true`, and the type model `IsClosed => Family._offer is null && kind.IsClosed` forces `IsClosed=false` once an `Offer` static is set. So bool's closed-ness genuinely needs the kind (or a type-level "closed" hook — a locked-core addition to `type.@this`). **Reverted; bool's kind stays.** This is the "tell me what needs it as a kind" case: it's `IsClosed`.

2. **Nested-kind read — `KindNestsTests` (ruling 1).** `{list, kind:{list, kind:path}}` must read through the type serializer, the outer's `.kind` a nested `type.@this` (Name=list, its `.kind.Name`=path). Today `type/serializer/Reader.cs:33` reads `kind` as a bare string and trips on the inner `{`. The `.kind` slot is typed `kind.@this`, and a nested `{list,kind:path}` is a `type.@this`, so nesting needs the inner wrapped as a kind and the serializer to recurse — a locked-core type-system design (how a kinded type sits in the `.kind` slot, how the element kind carries a nested element's own kind). **Needs the architect's design call** before I touch the locked serializer/`type.@this` construction. (Was red at the inherited baseline too — a pre-existing stage-3 TODO, not a regression.)

3. **The app-wide kind search has live callers.** The demolition lists `type/list/this.cs:50–51` (the `Kind(type,kind)` fallback) and `:57–69` (`Held`) + the one-arg `Kind(string)` as going. But they have live production callers: `type/reader/this.cs:112` (binary→narrow by name), `text/kind/this.cs:25` (the `*format` coiner), `module/llm/code/OpenAi.cs:741` (format by MIME), and the `{binary, X}` narrowing through the two-arg fallback. These are cross-type "find a format kind by name/MIME" uses — distinct from "ask a type for its kind, fall back to the app" (the principle in the plan). Removing them as written would strand these. **Which:** keep `Held`/`Kind(string)` for by-MIME lookups and only drop the `Kind(type,kind)` fallback, or refactor the callers? (The text coiner is a stage-4 target anyway.)

4. **`type.format` filter removal needs test surgery.** The per-type `.format` filter (`type/this.cs:126` + `type/format/this.cs`) is production-unused except `FormatKindTests` (via `%!app.type.X.format.list%`); no `.goal` uses it. Removing it is clean C# but requires rewriting the FormatKindTests that assert through `.format` to ask the kind list directly. Do this now, or is it leftover-stage (6)? (Not the serializer `app.type.format.*` or the format-in-play `%!app.type.format%` — both stay.)

5. **Blocked on Ingi (held).** `separator` (Q2, held — becomes `kind<text>`; the 2 test `.pr` count was sent) and `JsonIsItemsKind` (its own goal says "held pending Ingi" — `{item, kind:json}` lands as item not text). Their tests stay red until Ingi rules.

6. **template has no parent value type.** The template kind (`template/kind/`) is a fill-strategy mark, not a kind *of* a value type — there is no `template.@this`. `kind<T>` needs a `T : item.@this`. Left on the non-generic base + `Owner => "template"`. Is template its own standalone kind (a host/infra kind, kept), or should a `template` value type exist so it's `kind<template>`?

7. **The kind/strict plang tests** (`SetAsImageStrictNoKind`, `SetAsTextSlashMarkdown*`, `SetAsImageGifStrictMismatch`) were red at the stage-2 baseline and remain red — strict magic-byte verification behaviour, which the structural migration doesn't change. Likely stage-4 (a format owns its bytes) rather than stage 3. Confirm scope.

## What stays (guard, verified)
Every type's name + `[PlangType]`; the serializer `app.type.format.IWriter/IReader/IOutput`; the format-in-play `%!app.type.format%` (`type.root`); `Kind(System.Type clr)` (host lookup, until 673); the C# host kinds json/dict/list/reflection.
