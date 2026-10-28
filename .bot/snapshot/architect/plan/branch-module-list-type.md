# Next branch: a module's own list is a type (`module-list-type`)

Written on snapshot (decision 231), to be cut from runtime2 after snapshot merges. It changes the locked type system, so nothing is built before Ingi's yes.

## Why

A module owns its own types (CLAUDE.md, "new needs go to the edge"). A module's own list, a `list<T>` subclass, isn't one. The type list names every `list<T>` subclass as the family `list` with kind `T` (`type/list/this.cs:193–201`, through `ContainerFamily`). So a setting or an action parameter typed with it is made as a plain `list<T>`, and the subclass's own reader (its `ICreate`) is never asked. The setting binder makes slots that way (`type/item/setting/this.cs:225, 255`).

Found on snapshot: Ingi wanted `debug.setting.variables` to know how to watch. The list couldn't be the setting's slot (TypeConversionFailed, "cannot lower a List<Object> into variable.list"), so `Watch` went on debug's setting instead (231).

## The goal step

```
plang --debug.setting.variables=["trace","goal"]
```

**Today (snapshot 231):** `Variables` is `list<text>`. Debug's setting reads each text as a variable inside `Watch`, and `Variables(sb…)` parses every name again at each step.

**After:** `Variables` is debug's own `variable.list` (`module/debug/setting/variable/list/this.cs`). Each name is born a variable once, when the setting is read, and the list watches itself:

```csharp
setting.Variables.Watch(user, this);
```

## The change

`PlangName` names a class that declares its own plang type (`[PlangType("…")]`) by that declaration, before the container-family rung. Its entry is its own, so its reader is its own, and it still answers as a list. A plain `list<T>` subclass with no declaration is named as today.

OBP pattern: a type per kind, decided at birth; new needs go to the edge.

## Open, for Ingi

1. How a module's list shows in text: its own name, or `list<variable>`.
2. Whether a module's type may take a name a core type already has.

## Tests (each red without the change)

- A setting typed with a module's list reads from the CLI's array through the list's own reader.
- An action parameter typed with it does the same from a `.pr`.
- A `list<T>` subclass with no declaration is still named `list` with kind `T`.

## Goes, once it lands

- `debug.setting.@this.Watch`: it moves to the list.
- The per-step parse in debug's `Variables(sb…)`: it walks the list's variables.
