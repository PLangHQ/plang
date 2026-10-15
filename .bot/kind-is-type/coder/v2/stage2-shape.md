# Stage 2 shape — settings are settings, not kinds (ruling 7)

Trace-first, before writing. Decision 774 stage 2: each settings class is its own top-level type named
by its namespace; a nested class read through its owner; a saved row read into its class by its key;
the `setting` family gone.

## What I traced

- `setting.@this` is `[PlangType("setting"), Kinds]` (`setting/this.cs:12`) — a type WITH kinds. Each
  settings class is a **kind of `setting`**, named by its `Path`.
- **Path logic already there** (`setting/this.cs:23–32`): namespace → `llm.setting`
  (`app.module.llm` → `llm`), `app.type.item.X` → `app.type.X.setting`; a **nested** class
  (`DeclaringType`) → `{ns}.{owner}.{declared}` → `llm.decider.setting`. (This IS the
  nested-class-through-its-owner fix, already on the setting class.)
- A setting's `Type` = `{setting, kind: <path>}` (`:59–63`) — the **kind (path) is what tells a saved
  row which class to read back**.
- Enlistment: `Registry.cs:330–334` holds `new setting.kind.@this(instance, this)` under `setting` for
  each settings class.
- The store is keyed **`<actor>!<path>`** (`actor/setting/this.cs:189`), options-only. `Class(path)`
  (`:330–331`) = `type.list["setting"].kind[path] as setting.kind.@this` → the class; callers use
  `Class(path)?.Create()` (a fresh instance), `?.Private` ([Own]).

## The shape

1. **`setting.@this` drops `[Kinds]`.** Settings are no longer a kind family. The base stays: a bare
   node `setting.@this(path)` for a module/action with no class of its own (`%!llm.query.setting%`),
   a value of the `setting` base type.
2. **Each settings class is a top-level type named by its Path** (`llm.setting`, `llm.decider.setting`,
   `app.type.size.setting`). The existing Path logic becomes the type's NAME; discovery registers each
   settings class top-level with that name — replacing `Registry.cs:330–334`.
3. **A setting's `Type` = `{<path>}`** (its own name, no kind) — `%!llm.setting!type.name%` is
   `llm.setting`. A bare node's Type = `{setting}`. The stored row (keyed by path, options-only) is
   unchanged — no stored-row shape change.
4. **`Class(path)` resolves through the type registry**: `type.list[path]` → the settings type → its
   ClrType → the class. A saved row reads into its class by its KEY (the key's path finds the class);
   old rows (keyed `<actor>!<path>`) read unchanged — a row's kind is no longer the class-picker.
5. **Nested through owner**: `app.module.llm.decider.setting` → `llm.decider.setting`, read through
   `decider`; no clash with `llm.setting`.
6. **Goes**: `[Kinds]` off `setting.@this`; `type/item/setting/kind/` (the setting.kind class);
   `Registry.cs:330–334`; the row's-kind-as-class-picker.
7. **Carry-forwards (your two notes)**:
   - Drop `virtual` from `type.@this`'s public facts (Property/Values/Alias/Description/Example): with
     no setting kind overriding the public member, a kind answers ONLY through the `Own*` hooks — one
     door per fact.
   - `setting.kind.Private` (reads `[Own]`) and the actor's `Own(path)`: the `[Own]` fact moves onto the
     settings class (or its type) with ONE name everywhere.

## OPEN (your call before I write)

1. **Naming a settings class by Path.** `NameOf` names by the namespace tail — `app.module.llm.setting.@this`
   → "setting" (clashes across modules). A settings class must be named by its Path (`llm.setting`).
   Proposed: the settings discovery registers each class top-level with Name=Path (the Path logic),
   not through NameOf's tail. Agree — or should a settings class carry `[PlangType(<path>)]`?
2. **The bare `setting` node** (`%!llm.query.setting%`, no class): stays a `setting.@this(path)` value
   of the base `setting` type (PlangType "setting" kept, now with no kinds). Confirm the base type stays.
3. **`type.list[path]` for a settings type** — the registry resolves `llm.setting` / `llm.decider.setting`
   to the top-level settings type. Confirm settings names live in the same top-level space (unique there,
   like every type), so `Class(path)` is a plain registry lookup.

Tests to green: SettingClassIsAType (red→green), OldSettingRowStillReads (guard, green), prompt C byte-equal.
No code written yet.
