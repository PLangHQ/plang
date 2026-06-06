# Stage 7 / constraint-lock — continuation (read after context clear)

## Where the branch stands (all committed, C# suite 100% GREEN)

Born-native is **done and green**. Everything below the `where T : item` hard
constraint is finished:

- **Born-native construction**: `UnwrapJsonElement` emits wrappers
  (`text`/`number`/`bool`) + the `null.@this` singleton; `%var%` refs stay raw
  strings (WrapTextLeaf, guarded by the source-gen `VarRefRegex`); dead
  `UnwrapNewtonsoftToken` deleted.
- **Serialization**: each scalar wrapper rides the wire bare. `item.IsLeaf`
  (scalars override true) → Normalize passthrough is ONE check. `item.Write(IWriter)`
  (OBP Rule 9) → `json.Writer.Value` is ONE `leaf.Write(this)` dispatch. Each
  wrapper has a `Json.cs` STJ converter (raw-STJ projection, dict/list precedent).
- **Conversion leaf**: `item.ToRaw()` virtual — one unwrap; absorbed the dict/list
  branches. `Variable.GetValue` unwraps scalars (collections stay native).
- **Everything `: item`**: scalars, dict/list, path/image/code, Variable
  (record→class), Ask, snapshot.
- **ScalarComparer collapsed**: Name()/IsDateTime/ToOffset/date arms gone; numeric +
  string + thin same-typed IComparable fallback (bool excluded — equality-only).
  **Mediator** (`Operator.NormalizeTypes`) inspects wrapper types; coercion runs
  BEFORE Compare's self-dispatch so `"5"==5` reconciles.
- **New types built**: `binary` (`: item`, wraps byte[], base64) and
  `choice<TEnum>` (`: item`, the first-class "enum" — typed via implicit op to
  TEnum, validates vs the enum's names, `[Choices]`-aligned).
- **C# stubs**: all 23 implemented. The 5 constraint tests are currently
  **structural lattice tests** (verify every value/domain type `: item`; int/Data
  not) — they pass NOW and pin the invariant the hard constraint will enforce.

## What REMAINS = turn on `where T : item` (the 1742-site cascade)

Adding `where T : item` to `data.@this<T>` (revert: it was on `data/this.cs`
~line 1468, then removed) produces **1742 errors / 101 handler files**. The
offending `Data<T>` slots, with Ingi's DECISIONS (do these):

| Offending T (count) | Decision |
|---|---|
| `string` (478) | → `Data<text>` |
| `bool` (286) | → `Data<bool>` (the wrapper) |
| `int`/`long`/`double` (208) | → `Data<number>` |
| `TimeSpan` (14) | → `Data<duration>` ; `DateTimeOffset`/`DateTime` → `Data<datetime>` |
| enums: `HttpMethod`/`PrecisionMode`/`OverflowMode`/`Trigger`/`ErrorOrder`/`ContentAs`/`StreamFormat`/`hash` (~150) | → `Data<choice<TEnum>>`. Handler reads `param?.Value` (implicit op → TEnum). Conversion leaf must build `choice<TEnum>` from a text literal (Enum.TryParse ignorecase + validate vs names) — ADD this arm to `type/catalog/Conversion.cs`. |
| `byte[]` (28) | → `Data<binary>` |
| `Dictionary<string,object?>` (40+), `Dictionary<string,string>` | → `Data<dict>` |
| `List<string>` (34), `List<X>` (various) | → `Data<list>` |
| `object` (26) | → `Data<item>` |
| **Domain objects** (GoalCall 66, Identity 52, actor 30, goal 24, step 24, sign 18, mock 14, BuildResponse 12, permission 10, Results 8, action 6, StatInfo 6, hash type 6, KeyPair 4[record→class], setting 4, list.type.list 20, step.actions 8, builder.type 2) | make each `: item.@this` (elevate the C# class to a PLang type). Records→class. Watch base-class clashes (most are `: interface` only — add item as the base). `IBooleanResolvable` methods → `override`. |
| `app.type.@this` (the `type` entity, 6) | decide: `: item` or bare Data. Probably bare Data (a "type" isn't a value). |
| `Operator` (32) | likely `[Code]` or bare Data (it's behavior). Investigate. |
| `Assembly` (4), `HttpContent` (6) | **de-Data** — Ingi: these never reach PLang, shouldn't be `Data<T>`. `path.LoadAssemblyAsync` and http upload-content use `Data<T>` as a `Result<value|error>` carrier. Return a plain C# `(T?, IError?)` tuple (or a tiny `Result<T>`); the plang-facing handler builds the PLang Data from the OUTCOME, never putting Assembly/HttpContent in a Data. |
| `T`/`TResult` (48) | thread `where T : item` through the ~25 generic infra methods (Merge/Clone/Ok/Fail). |

### Sequence
1. Domain objects → `: item` (green-keeping, no constraint yet). Batch ~17 types.
2. De-Data Assembly/HttpContent (green-keeping).
3. Add `choice<TEnum>` conversion arm to Conversion.cs (text→choice, validate).
4. Swap all handler `Data<rawCLR>` slots per the table — AND fix each handler BODY
   that reads the param (it's now a wrapper; unwrap via `.Value`/ToRaw at the leaf).
   This is a second body-sweep, per handler.
5. Add `where T : item` to `data.@this<T>`; thread through the generic infra; fix
   until it compiles.
6. Flip the 5 lattice tests to also assert compile-enforcement (the double-wrap
   `Data<Data>` and `Data<int>` must not compile — a negative-compile fixture).
7. Both suites green.

## Design decisions locked (from Ingi this session)
- **`choice`** (not `enum`) — layperson term, aligns with `[Choices]`. Generic
  `choice<TEnum>` keeps typing; implicit op → TEnum; one `.Value` at use.
- **`binary`** type for byte[].
- Dictionary/List → dict/list.
- Domain classes → `: item` ("elevating a C# class to a PLang type, like `: object`").
- Assembly/HttpContent → NOT Data (internal results → tuple/Result<T>).

## Follow-up (logged in Documentation/Runtime2/todos.md, NOT this branch)
- **Serialization centralization part 2**: `Normalize`-on-item virtual (read-side
  mirror of the `Write`-on-item already done) — collapse the NormalizeValue switch.
- **PLang-named errors**: replace `GetType().Name` in error messages with the PLang
  type name (the collapsed ScalarComparer dropped the old `Name()` switch).
- **Channel.Resolve**: the leaf is `Resolve`, not the generated action code — move
  the wrapper→string unwrap into the consumer (OBP); discuss.
- **Per-path lazy narrowing** of materialized json (whole-tree-on-first-touch today).
- **Option B**: single JsonConverterFactory at `data` layer + PLang-native wire
  attrs (replace `[JsonIgnore]`/`[JsonConverter]` so protobuf needn't know STJ).

## Mechanics (verified this session)
- Rebuild: `dotnet build PlangConsole` (generator changes need full rebuild).
- C# suite: `dotnet run --project PLang.Tests --no-build` — `--no-build` reads a
  STALE PLang.dll unless you `dotnet build PLang.Tests` first (which rebuilds the
  PLang.dll dep). The early "23 stubs / no regressions" reads this session were a
  stale-binary artifact; always rebuild before trusting `--no-build`.
- PLang suite (Task 6, NOT started): author + build the 31 stubbed
  `Tests/ScalarsAsNative/Stage{2..7}/*.test.goal` to the test-plan, build green;
  fix `DictIsItemKeepsNoOrder` (lazy-narrow). Both suites to 100%.
