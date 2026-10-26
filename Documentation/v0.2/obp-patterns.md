# OBP patterns: spot the shape before you write it

`obp-smells.md` names what's wrong once it's written. This file is the other half: **when the situation looks like this, the OBP shape is that, and here is the tell that you're drifting away from it.** Every proposal and every review names the pattern it follows, or the smell it risks, on its first line.

Each pattern comes from a real case: a design that was proposed, reversed, and settled. New cases add rows; a miss in a drill becomes a pattern.

## 1. Something must be saved and read back

**Shape:** the object writes and reads itself. A view (`Store`, `Out`, `Snapshot`) picks its tagged members (`[Store]`, `[Out]`, `[Snapshot]`), each member writes itself, and a read sets each member by its own type. Map the instances as they are, each member named what it is: read, map, set.

**Tells:** a second type holding a copy of an object's state; someone other than the object packing its saved form or unpacking it by key; grouping or flattening instances into a shape that doesn't exist live; a new name for a part of an object; a new concept beside an existing view or tag.

**Case:** a call frame's saved form. Proposed and reversed: a `call.snapshot` value (stored twice), a `place` member (an invented name), one entry per goal call with a list of steps (a shape that doesn't exist live), a `[Key]` tag (a concept beside a view). Settled: each frame is saved as it is, through a `[Snapshot]` view and its tag, like `[Store]` and `[Out]`. Ingi: "you just read and map and set the value, we don't need to care how it mapped it just works."

## 2. One class behaves differently by case

**Shape:** a type per kind, decided once at birth by whoever knows. Each kind does its own part (a virtual), and nothing downstream asks again.

**Tells:** `x == null`, a sentinel (`Place < 0`), a guess from which fields are empty (`IsGoal => Goal != null && Step == null`), a ternary on a field inside a method.

**Case:** goal, step and action frames were one class, so resume branched on null fields. Settled: `call.goal`, `call.step`, `call.action`, each with its own rest. The same sentinel then reappeared inside `call.action` (`Place < 0` for an action composed in C#), and became a kind of its own.

## 3. A choice made inside a registry

**Shape:** the registry selects and owns lifecycle; the behaviour sits on the element. The element that knows decides.

**Tells:** a registry or stack asking another object's field to pick what to make (`action.Step is {} ? A : B` inside `Push`); `is X` inside a collection's method.

**Case:** the stack's `Push(action)` picking the frame kind from `action.Step`. The action is the one that knows.

## 4. Finding something by a number or a name

**Shape:** whoever assigns the number or name answers the lookup.

**Tells:** a second indexer beside an existing one, meaning something else; a recursive search through children to match a number someone else assigned.

**Case:** a step's index is numbered goal-wide by the goal, across its bodies. The lookup was placed on the step list beside its positional indexer. Settled: the goal answers `goal[index]`, and the list's indexer stays positional.

## 5. "Where am I?" / "what comes after me?"

**Shape:** a fact the element holds from birth, set by whoever makes it, as a step is born knowing its goal.

**Tells:** a search by reference from the root to find the element's own container or position.

**Case:** a step's frame found "the rest after me" by searching from the goal's top list through every body. Settled: a step is born knowing the list that holds it (`step.List`), and its rest is that list's, from its position.

## 6. A compound name

**Shape:** write it as a dot path and check that it navigates. Each segment is an owner whose member is the next.

**Tells:** a glued name for a fact of a thing the object holds (`PrPath`, `goalName`, `stepIndex`). The first segment is usually an object waiting to exist.

**Case:** `goalName`, `stepIndex` became `goal{name}`, `step{index}`. `prpath` was missed even then. Ingi: "it should be pr:{path}". The goal's `.pr` is a file, and a file in a snapshot writes its path, so `pr: {path}` falls out of pattern 1.

## 7. One new need beside an existing mechanism

**Shape:** reuse the one door every path already passes, once. Don't add a parallel concept, and don't edit every caller.

**Tells:** a new tag that means what a view's tag already means; a fix that needs edits at dozens of call sites; "and the next one written would miss it".

**Case:** the snapshot view needed a say before a value's own write. Not 103 call sites, and not `View.Write` (which isn't the one place): item's non-virtual `Output`, which every write passes.

## 8. Comparing or deciding by a value's contents

**Shape:** the value answers. The operands ride whole.

**Tells:** `.ToString()` on both sides of a comparison; cracking a carrier into primitives to pass to a helper.

**Case:** resume compared `identity.PublicKey` with `signature.Identity.ToString()`. Settled: `Identity.Made(signature)`, which compares its own key exactly (text equality ignores case, which is wrong for a key).

## 9. A reader that answers another type

**Shape:** refuse it. The text would lie about what it holds.

**Tells:** "its reader answers a node", "decode it as X but give back Y", a door to open a typed value as something else.

**Case:** a `call` row read as a snapshot node; a door to open any typed source as a dict. Both refused, because any courier could then open any carrier past its type.

## 10. A setter that changes other fields

**Shape:** it's one concept held in two fields, so one birth method sets it, and the setter (if any) calls that method.

**Tells:** "a setter with a side effect on another member".

**Case:** a frame's born names are held in `_variables` and `_arguments`; one private birth method serves the constructor and the read.

## How to practise

- **Name the pattern first.** Every proposal and review opens with the pattern it follows or the smell it risks.
- **Drills.** Take a past decision from a branch's log, hide the answer, solve it by pattern, then compare with what was settled. A miss becomes a new row here.
- **Reviews are practice.** Each finding in a review names its pattern by number.
