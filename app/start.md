# app

`app` is the root. A program is a file. `app` boots it.

```csharp
app.start(Data<path> entry)
  → new file(entry).start()
```

Everything in plang is reachable from `app`. Every concept is owned once, here.
Nothing constructs a sibling — it asks `app`.

## execution chain

```
app.start(entry)
 └─ file.start()              loads start.goal
     └─ goal.start()          → step.list.start()   run all steps
         └─ step.start()      → action.list.start() run all actions
             └─ action.start()   e.g. new db(Data<Db> db).start()
```

Every `start()` asks one question: *what does this want to do?* A goal wants to
run all its steps — that's the **list** — so it starts the list. See [obp](obp/start.md).

## owns

| concept | `Data<T>` | role |
|---------|-----------|------|
| [file](file/start.md) | `Data<path>` | a `.goal` file on disk |
| [goal](goal/start.md) | `Data<text>` name, `Data<path>` location | named step sequence |
| [type](type/start.md) | `Data<text>` name | the value kinds |
| [channel](channel/start.md) | `Data<text>` name | where output goes |
| [identity](identity/start.md) | `Data<text>` name, `Data<text>` publicKey | who is running |
| [signing](signing/start.md) | `Data<text>` data → `Data<text>` signature | data integrity |
| [llm](llm/start.md) | `Data<text>` prompt → `Data<text>` response | language model |
| [translate](translate/start.md) | `Data<item>` value, `Data<type>` target | format conversion |
| [error](error/start.md) | `Data<text>` message | what to do when things fail |
| [warning](warning/start.md) | `Data<text>` message | what to flag without stopping |

`app` is the program. One run of it is a [context](context/start.md) — the
per-run state (variables, current goal/step). Context is navigated through
`IContext`, never passed.

## OBP rules

- **Ownership is here.** Every concept lives once, in `app`. No concept constructs
  a sibling — it receives what it needs.
- **Data in, Data out.** Every method boundary uses `Data<T>`. No raw `string`,
  `int`, or `bool` crosses a method signature.
- **Behavior flows down.** `app` starts `file`, `file` starts `goal`, `goal`
  starts `step`, `step` starts `action`. Nothing flows back up.
- **The list owns the loop.** `goal.list.start()`, `step.list.start()`,
  `action.list.start()` — the list iterates, the element does one thing.

## source

`app/start.cs`
