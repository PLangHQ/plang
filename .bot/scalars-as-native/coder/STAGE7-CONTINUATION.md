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

## PROGRESS on the constraint cascade (committed green, constraint still OFF)

- **Domain objects → `: item` DONE**: GoalCall, Identity, actor, goal, step, action,
  stepactions, sign, mock, BuildResponse, Results, hash, builder.type (classes);
  list, setting, StatInfo, permission (records→class). Build green.
- **KeyPair de-Data DONE**: `IKey.GenerateKeyPair() → (KeyPair?, IError?)`; KeyPair is
  a plain class (internal crypto result, never crosses PLang). Ingi confirmed.
- Census re-harvested AFTER domain :item → **1428 errors remain** (was 1742). The
  domain-type rows are gone. Remaining (turn constraint on at `data/this.cs` ~line
  1468/1473 to re-list): scalars string(478)/bool(286)/int(178)/long(18)/double(12)/
  TimeSpan(14); enums PrecisionMode(54)/OverflowMode(54)/HttpMethod(12)/StreamFormat(6)/
  ContentAs(6)/ErrorOrder(6)/Trigger(6)→choice; BCL Dictionary(40+2+2)/List<string>(34)/
  List<path>(10)/List<test>(8)/List<Identity>(8)/string[](6)/List<LlmMessage>(6)/
  List<action>(6)/List<GoalCall>(6)/List<Data>(6)/Dictionary<string,string>(6)/
  List<goal>(2)/List<List<Dict>>(2)→dict/list; object(26)→item; Operator(32)→[Code]/bare;
  type(6)→bare/item; Assembly(4)+HttpContent(6)→de-Data; generic T(46)/TResult(2)/
  List<T>(4)→thread; data.@this(2)→double-wrap kill (bare Data).

### PILOT VALIDATED — the `number` slot swap (reverted, but findings hold)
Bulk-swapped all `data.@this<int|long|double|decimal|float>` → `data.@this<number>`
across 26 production files (constraint OFF). Results:
- **Construction survives free**: `number` has FROM-raw implicit operators
  (`int→number` etc.), so `Data<number>.Ok(5)` and defaults compile unchanged.
- **Reads break: 46 production sites** — `number.@this` used as `int`/`double`/`long`
  in handler bodies (`(int)x.Value` casts, `x.Value < n` operators). number has
  EXPLICIT conversions, so each is a mechanical cast. ~18 files (list/http/llm/
  error.handle/test.run/math/timer/timeout/signing/etc.).
- **Source-generator fix needed**: generated lazy-param default emits `(number)-1`
  for a negative default (list.Add/Remove `AtIndex`) → CS0075 ("cast negative value
  must be parenthesized"). The Emission must parenthesize default exprs: `(T)(expr)`.
- **DO NOT add implicit TO-raw on number** (or FROM-raw on text/bool): bidirectional
  implicit makes `number == 5` / `text == "x"` ambiguous (CS0034) and breaks existing
  comparisons. Reads stay explicit casts.
This pattern repeats per scalar category (string→text, bool→bool, TimeSpan→duration:
construction needs `new wrapper(raw)` since they LACK FROM-raw implicit + the `==`
ambiguity blocks adding it; reads need unwrap). Budget the generator fix + per-site
sweep per category; turn the constraint on only after all categories + BCL + enum +
de-Data + generics are swapped.

### The big remaining grind (scalar/BCL/enum slot swaps)
- **75 handler files** declare scalar `Data<rawCLR>` props. Each needs the property
  type swap (`@this<string>`→`@this<text>`, `<int>/<long>/<double>`→`<number>`,
  `<bool>`→`<bool>`, `<TimeSpan>`→`<duration>`) AND a per-handler **body sweep** —
  the body reads `.Value` which is now a wrapper. text.@this mirrors much of the
  string API (Length/Contains/etc.) + has `implicit operator string`, so many reads
  survive; `==`/`switch`/`string.IsNullOrEmpty(x.Value)` and number→int reads
  (`(int)x.Value` / `x.Value.ToInt32()`) need touching. This is the bulk — budget
  per-handler care, not a blind find-replace.
- **Assembly de-Data is intricate**: `path.LoadAssemblyAsync()` (base + file override
  + 4 callers incl. code.load, module.add, code.Snapshot) uses `AuthGate` whose
  exit-bubble returns a `Data` — the tuple conversion must preserve the Exit-typed
  early-return. HttpContent: ~10 internal helpers in http/Default.cs + callers.

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

## PLang RUNTIME suite status (born-native integration — checked this session)

Born-native (committed, C# green) introduced PLang-runtime regressions the C#
component tests didn't catch. Ran `cd Tests && plang --test`:
**241 pass / 31 fail (non-stub) / 35 stale / 2 skip** (baseline pre-born-native ~271).

**FIXED this session** (conversion/serialization/condition seams unwrap item via ToRaw):
- number.Convert + number.FromObject (text→parse), duration.Convert (text→parse),
  assert.Compare (wrapper→raw before numeric/IComparable), Text.cs serializer
  (scalar leaf → bare ToString, not JSON-quoted), Operator.Contains/IsEmpty
  (unwrap text). 234→241.

**REMAINING 31 (non-stub) — clusters, each needs per-case work:**
- **bool-casing test-pins** (born-native bool.ToString = "true"/"false" lowercase;
  tests assert "True"/"False"): ActorSwitch, SetAsImageStrictNoKind,
  SetAsTextSlashMarkdownStrictUnverifiable, Mock — UPDATE the test expectations to
  lowercase (born-native is correct).
- **build-time type inference** (TypedReturns Stage0/4): TestUserTypeHintWins,
  TestLlmQuerySchema(json), TestFileReadCsv, TestFileReadMissing, TestBuildMethod —
  "object" vs "json/csv/txt". Born-native may have shifted type tagging; investigate
  the build-time `→ returns` annotation path (or stale .pr — rebuild first).
- **test-module meta** (TestTag, TestDiscover×2, TestConditionIfRecordsBranchIndex):
  likely born-native value-in-test-result or stale .pr.
- **residual quoting**: File.test, MultipleModifiersCompose, AssertComplete,
  Permission/Start still show `"..."`-quoted output — the Text-serializer fix didn't
  cover this path (the `save`/write path or a different serializer); root-cause the
  save→file write for a text value.
- **actor conversion**: Channels/Set/ExplicitActor — `channel.set Actor "system"`:
  Cannot convert String→actor.@this. actor is now :item; wire the string→actor
  lookup conversion (or the channel Actor param read).
- **condition edge**: ConditionSubStepsTrue, ConditionNot, NavigationOnTypeUnknown.
- **misc**: Cache, OnErrorMultilingual, ErrorOrdering, Audit.
- **35 stale .pr**: rebuild (`plang build`) before trusting — some may be passing.

**Approach for the remaining**: same as the fixed ones — find each `is string`/raw-CLR
read of a now-wrapped value and unwrap via item.ToRaw/.Value at that leaf, OR update
a test that pins old raw rendering (bool casing). Rebuild stale .pr first to clear
false failures. THEN the scalar-swap constraint cascade (separate, documented above).

## PLang runtime suite — UPDATED status (this session, all committed)

**234 → 252 / 309 pass** (35 stale = the unbuilt ScalarsAsNative stage goals = Task 6;
2 skip). Fixed the architect's flagged seams + more, each root-caused (not patched):
- number.Convert/FromObject, duration.Convert: unwrap item→ToRaw at entry.
- assert.Compare AND assert.AreEqual: unwrap item→ToRaw (bool/text compare by value,
  not string — killed the "True" vs "true" cluster).
- Text.cs serializer: scalar leaf → bare ToString (not JSON-quoted).
- **save/write quoting was actually a NEWLINE seam**: born-native moved save<string>
  off the raw-string WriteAllText path onto the channel Text serializer (appends \n).
  Fixed FilePath.Save to unwrap text/binary and write bare (no \n). [Deeper: Text.cs's
  \n is a channel concern, not serialization — unify the two write seams later.]
- **actor conversion-arm**: actor.Convert(name→App.GetActor); self-wires via OwnerOf.
  The pattern for any name-reached domain type now :item (goal/etc. if they surface).
- Operator.Contains/IsEmpty: unwrap text wrapper.

**REMAINING ~17 (non-stub), need runtime --debug tracing or are a separate subsystem:**
- **condition-logic** (ConditionSubStepsTrue, ConditionNot, TestDiscover×2, TestTag,
  TestConditionIfRecordsBranchIndex, Audit): `if %flag%` builds as `%flag% == true`;
  STATIC ANALYSIS SAYS IT SHOULD PASS with the fixes (bool.@this==bool.@this via
  bool.AreEqual). It fails anyway → a subtle interaction I could not pin without a
  runtime trace. NEXT: run one with `plang '--debug={"goal":"Start"}'`, watch %flag%'s
  type+value into condition.if and the Equal()/AreEqual dispatch. Could be `set %flag%
  = true` not producing bool.@this, or the sub-step disable path. SubStepsFalse PASSES
  (false path), so it's specific to the true/enabled path.
- **TypedReturns build-inference** (5: object vs json/csv/txt): build-time `→ returns`
  type tagging — a DIFFERENT subsystem from born-native runtime value-flow. Likely
  pre-existing or needs the object→item fold (part of the constraint cascade, not done).
  Architect: root-cause, don't patch the expectations.
- **test-specific**: AssertComplete / ErrorOrdering show `Expected: ""world""` (embedded
  quotes in the expected) — inspect the goal; NavigationOnTypeUnknown (over-eager true).
- **Variable 'default' is not a list** (Mock): a list op on a non-list — born-native dict/list.

Architect's ordering still holds: get these green + trustworthy BEFORE the scalar-swap
cascade (the compiler-blind body-sweep needs this backstop). C# suite stays 100% green.

## ROOT-CAUSE NAILED: the condition cluster is STALE .pr, not a born-native bug

Deep-traced ConditionSubStepsTrue (env-var/throw probes through if.cs → steps loop →
assert). Finding: the born-native condition CODE is correct — the current .pr copy
evaluates `if %flag%` (flag=true) to res=True and runs the children. The FAILING copy
is a DUPLICATE test tree (`Tests/Condition/*` alongside `Tests/Modules/Condition/*`)
whose .pr is STALE OLD FORMAT:
  - stale: `Operator type="string"`, `defaults: null`
  - current: `Operator type="operator"`, `defaults:[{negate,false}]`
The stale .pr evaluates the condition false → children skipped → assert fails. NOT a
code regression. (`set %flag%=false` also showed an inconsistency: sometimes raw
Boolean, sometimes bool.@this — a minor variable.set-bool wrapping gap, separate.)

**So the PLang "regressions" are mostly STALE/DUPLICATE .pr** (condition cluster,
TestDiscover, TestTag, etc. — old-format .pr in `Tests/Condition/*` and other pre-
restructure trees). The architect's PREREQUISITE was exactly right: **rebuild stale
.pr FIRST** (`plang build`, LLM). Many will go green on rebuild — they are not
born-native code bugs. The genuinely-code born-native bugs (conversion/serialization/
save-newline/actor/assert seams) are ALL FIXED (234→252).

**Revised remaining (after a stale rebuild, est.):** TypedReturns build-inference (5,
separate subsystem — object vs json/csv, needs the object→item fold or is pre-existing);
a couple edge cases (NavigationOnTypeUnknown over-eager true; AssertComplete/ErrorOrdering
quoted-expected — inspect goals); the `set %x%=<bool>` wrapping inconsistency (minor).

NEXT: `cd Tests && plang build` (rebuild stale duplicates) → re-run → the count should
jump; then the scalar-swap constraint cascade with a trustworthy runtime backstop.
