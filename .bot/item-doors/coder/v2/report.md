# item-doors — stage 1 report (coder)

Stage 1: a value is born whole — every plang value holds the one type object its app made for it, handed in by its
maker; no value builds its own type, and every kind is born holding the type it is a kind of.

## Stage-1 tests (decisions 8, 9)

| test | kind | state |
|---|---|---|
| `ActionSlotIsByTypeTests` | C# red | green |
| `ImageKindKnowsItsMimeTests.ImageFromBytes_Kind_CarriesItsRegisteredMime` | C# red | green |
| `TextKindIsTextByItselfTests.PlainText_Kind_IsText_WithoutNameFloor` | C# red | green |
| `/app/type/kind/strict/SetAsImageStrictNoKind` | plang red | pass |
| `/app/type/kind/strict/SetAsImageGifStrictMismatch` | plang red | pass |
| `/app/type/kind/strict/SetAsTextSlashMarkdownStrictUnverifiable` | plang red | pass |
| `/app/type/kind/strict/SetAsImageGifStrictMatching` | guard | pass |
| `/plan/item-doors/app/type/image/BytesKnowItsMime` | guard | pass |
| `/plan/item-doors/app/type/text/NamedTypeIsRegistered` | guard | pass |
| `/plan/item-doors/app/type/text/TextKindIsText` | guard | pass |
| `/plan/item-doors/app/type/dict/key/KeyIsTheFirstKey` (39) | added | pass |
| `/plan/item-doors/app/goal/RunningGoalIsAppGoal` (62/63) | added | pass |
| `InputTypeTests.AClicksModifier_IsTheFirstKeyHeld_AndItsListIsEvery` (41) | added, C# | green |

The three strict reds needed the builder too (the slash forms dropped kind and strict) and three runtime fixes:
an image holds its declared type (b0cf849dc, b8813dd03), a type with a kind and strict builds (aa8bb032f), and a
path-backed image reads nothing at the set (3956a8e09).

Whole suites (after 79): C# — every suite green except Wire's six snapshot/resume reds, red on runtime2 before this
branch. plang — 303 total, 277 pass, 11 fail, 12 stale, 3 skipped; the 11 are the long-standing reds (Cut2, Cut5,
DataTypeReadsEntity, Narrow_ChainWideBangBothBranches, Plane_DataKeyVsProperty, BareLiteralJudgedByForm,
ListIsItemSortsAsBefore, goalchannelrecursion, Failure_DictEqNumber, Failure_DictOrderDict,
DoublePlusDecimal_Errors), none new.

## What stays marked

- **The C# implicit operators (50), their own branch after item-doors** — each birth with no app in reach keeps a
  class-built type, marked "until the implicit-conversion branch": number's 15 kind statics (one class-built number
  type), duration's `short`, text's `string → text`, bool's `True`/`False`, choice's `T → choice` and a set's offers,
  `data<T>(T)`, size's `Standards` (a parse or count with no asker), `reflection.Fallback` (an item's write and dict's
  record lowering with no context), test.format's report kinds, `KindOfClass` (class-built types only). The
  context-less `item.Output`/`item.Clr` doors can take a non-null context only once no value is born without one.
- **error** — its makers have no app (the implicit-conversion branch).
- **event** — shared across apps (the event door, Ingi's).
- **template's kinds** — ownerless, built from their classes, until stage 6 gives template a type (71).
- **the memory's Debug form** — `%!app.variable%` in a dump writes its variables' values, not their names, until the
  memory writes itself (103, waiting on Ingi: the memory as a `list<variable>`, its rows through one door on list's
  base).

## What looked wrong (found on the way)

- **Test speed.** An app's birth is ~150 ms warm and ~90% of the suites' CPU: the type registry re-reads the
  assembly's metadata for every app. Ingi chose to live with it (no process-wide scan cache, no cloned registry);
  fix B (290aad619) trimmed Enlist.
- **dev.sh** stored a sweep's "tested" snapshot at its end, so an edit made during a sweep counted as tested
  (fixed, 6f4d0572d, Ingi).
- **The --debug core dump** — the app in its asker's memory walked forever (dcd7eb293), then 89 replaced the guard:
  the app writes itself and, met again, writes its name.
- **Backlog (architect)**: "this type, made strict" has two callers (image courier, the builder's Tighten) — a third
  makes it a type member; the strict imprint has two doors (image.Create, set.cs) — one door: the courier.
- **Interims in the code**: the memory's constructor builds class-built datetime/guid types for `Now`/`GUID` under a
  stale comment ("before the app's types exist" — the type list exists before the actors); worth its own small fix.
