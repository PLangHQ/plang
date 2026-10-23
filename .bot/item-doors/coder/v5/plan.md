# coder — item-doors — v5 plan: stage 6, traced and shaped (#6 settings, then #1b list rows)

Nothing built. The architect asked for this shape before any code; six decisions are open (below).

## #6 settings: the current tree

`actor/setting/this.cs` (440 lines) and `type/item/setting/this.cs` (323), plus the two node classes
`setting/action` (58) and `setting/module` (38). Everything on the plan's demolition list is still there.

| door | prod callers | test callers |
|---|---|---|
| `Of<T>()` | 29 | 53 |
| `Of(owner/type/Type/module/action)` | 6 (the `%!x.setting%` reads: `item:288`, `type:907`, `clr:120`, `node:107`, `module:203`, `action.Item:37`) | 0 |
| `Set(key, data)` / `Set(path, dict)` | 6 (Executor ×3, `app:378`, `test:175`, `item.setting.Write`) | 88 |
| `Save` / `Remove` | 4 / 1 | 8 / 1 |
| `Get(action, option)` | 2 (`setting.action:38`, `terminal/program:88`) | 10 |
| `Load()` | 5 (`app:465`, permission, identity) | 17 |
| `Apply` (setting's) | 2 (`goal/list:193`, the store) | some of 14 |
| `Copy()` on a setting | 2 (`goal/list:191`, `permission:190`) | 1 |
| `Rows(context)` | 1 (`step/this.Scope.cs:39` via `Fresh`) | 9 |
| `[Own]` | 0 classes (dead) | — |

## The shape (what stays, from the plan)

```csharp
// actor/setting/this.cs: a scope's data by address; the actor's rows read on first ask
private readonly ConcurrentDictionary<string, data.@this> _data;    // this scope's, whole, by address
public async Task<data.@this> Get(string address)                   // closest scope's, else the saved row
                                                                    // (user's, then system's), else NotFound
public async Task<data.@this<T>> Get<T>()                           // Get(T's address), made by T's type,
                                                                    // T's defaults for what it leaves out
public data.@this Set(string address, data.@this data)
public Task<data.@this> Save(string address, data.@this data)
public Task<data.@this> Remove(string address)
private string Key(string address)                                  // "{actor}!{address}", owned once
```

`type/item/setting/this.cs` keeps `Enabled` and `IsTruthy`; the whole reflection member system goes.

## Open decisions

1. **Eight sync readers versus an async `Get<T>`.** Read on use makes the first ask hit the store, so `Get<T>` is async.
   - Six readers have an async member one hop up and convert cleanly: `app.Name`/`Environment`, `debug.Setting`
     and `debug.WriteFiltered`, Fluid's `Writer`, `test.list.Actor`, `call.list.Setting` (read per push).
   - Two cannot:
     - `data/schema/signature.cs:104` reads `Expiry` inside the sync ref-struct json reader.
     - `size(bytes, context)` is a constructor reading `Standard`.

   Options:
   - (a) All async; those two get the setting value handed in by their async maker.
   - (b) Keep the rows read once at start; `Get<T>` stays sync.
   - Lean: (a).
2. **Cost without the built cache.** Hot readers: `number.Order` (each compare), text `Rendered`/`Encode` (each write), the call push.
   - Option (i): `Get<T>` keeps the T it made as that layer's data value, so a read is a lookup and a write replaces it. But this is what 1b removes for list rows ("keeps nothing").
   - Option (ii): convert per read, and measure it.
   - Lean: (i) for settings. A setting is one record per address, not a billion rows.
3. **The address is not a type name today.** Settings classes are held by the `setting` family "its own way" (`type/list/Registry.cs:346–347`). Their address is the namespace cut (`setting/this.cs:28–34`).
   - Making the address the type's name means each class gets a type named `llm.setting`, which is a locked type-system change.
   - The alternative: the cut is owned once on the setting type.
4. **The node classes go.** `%!llm.query.setting%` is `Get("llm.query.setting")`: what is stored, or nothing.
   - The action and the module each answer their own address; they already own `Setting`.
   - `Get(action, option)` reads the action's data member, then the module's, else the action's default.
   - Teaching rows: a class's `type.Property` (#4); a node's, the action's `Property`.
5. **No check on a set.**
   - `--test={"timeoutSeconds":5}` with a typo is stored silently. A program reading it gets T's default.
   - Today that typo is refused with UnknownSetting.
   - This is ruled (28); I'm flagging that a CLI-flag typo now goes quiet.
6. **`[Ask]`** stays llm-provider's, run inside `Get<T>`. Untouched here.

## #1b list rows: the current tree

- `_hasWrapped` now has **17** uses (the plan said 10).
- `Born`/`Keep`/`Element` (`list/this.cs:160–185`): a typed list (`this.Generic.cs:89`) makes a row at its first read and writes it back into the slot.
- The same-object exit is `list/this.cs:644`. The only `Clr<List<object?>>` callers (terminal program `:98`, `:121`) only read the rows.
- **The plan's shape misses `Chunk`** (`:26`): an extend holds another list whole in one slot (O(1)), read in place. Under one storage a read must go through chunk offsets. Lean: keep `Chunk` as the list's own storage; make-on-read applies inside it.
- **Cost:** a `list<path>` read twice makes the path twice. That is the plan's rule ("keeps nothing"), so it is noted, not changed.

## Order once ruled

#6 first, one commit per piece (store, readers, the node classes go), then #1b. Tests come with each piece.
