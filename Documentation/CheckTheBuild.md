# Check the build

A step's text says what you meant, while its `.pr` file records what the step will actually do. The builder turns each
step into one call of one module function, and the `.pr` file is that call. Two steps that read almost the same can
become different functions. A step that reads right can also lose a column or pick the wrong module. Nothing fails
until the step runs, and sometimes not even then.

So after every build, look at what the new steps became before you run the app on real data.

## Where the answer is

Each step has a file in `.build/<goal path>/`, named by its position: `.build/Data/03. select_product.pr`. Four fields
answer almost every question:

| Field | What it tells you |
|---|---|
| `Text` | the step as it was written |
| `ModuleType` | the module, e.g. `PLang.Modules.DbModule` |
| `Function.Name` | the function in that module, e.g. `SelectOneRow` |
| `Function.Parameters` | the values it gets, including `sql` for a database step |

List what every step of a goal became:

```
grep -E '^  "(Text|ModuleType)"|^    "Name"' .build/Data/*.pr
```

```
.build/Data/03. select_product.pr:  "Text": "select name, price from products where id=%productId%, return 1 row, write to %product%",
.build/Data/03. select_product.pr:  "ModuleType": "PLang.Modules.DbModule",
.build/Data/03. select_product.pr:    "Name": "SelectOneRow",
```

To see the SQL of a database step:

```
grep -A2 '"Name": "sql"' ".build/Data/03. select_product.pr"
```

## What to look for

| Step | Check in the `.pr` |
|---|---|
| `insert`, `update`, `select` | Every column you named is in `sql`. A step that names `qkey=%k%` and whose `sql` has no `qkey` lost the column without saying so. |
| `select ..., return 1 row` | `Function.Name` is `SelectOneRow`. With **one** column selected, the variable is the value itself: `%productName%`, not `%productName.name%`. |
| `select ...` (many rows) | `Function.Name` is `Select`; the variable is a list, so `%rows.Count%` works and `%rows[0].name%` is the first row. |
| `query file "x.sql"` | `Function.Name` is `QuerySqlFile` and `dataSourceNames` and `tableAllowList` are among the parameters. A step that became `ReadTextFile` reads the file as text. |
| `set cache` | `SetForAbsoluteExpiration` or `SetForSlidingExpiration`, and `timeInSeconds`. |
| `if ...` | `ModuleType` is `ConditionalModule` and the function matches the test, e.g. `IsEmpty`, `NotContainsString` for `does not contain`, `CompoundCondition` for `and`/`or`. |
| `throw error` | `ThrowErrorModule.Throw` with the status code you wrote. |
| `call goal X` | `CallGoalModule.RunGoal`, the goal name, and each parameter you passed. |
| A step that reads a value from the machine or a service | The function name is the one you meant, not a neighbour in the same module that returns something else. |
| A variable name as a value | The name keeps its `%`. A parameter `"samples"` where you wrote `%samples%` is a fixed text, not your variable. |

## When the build failed

A step that could not be built has no `.pr` file and fails when it runs. The build lists those steps at the end:

```
❌ BUILD FAILED - 1 step(s) did not build
   These steps have no .pr file and WILL fail at runtime.

   /FilesAndVars.goal:16
       if file "work/missing.txt" exists, write to %missingExists%
       → ... A one-line condition can only call a goal, end the goal or throw ...
```

Read the reason. It usually says which wording the module expects.

## When a step became the wrong thing

Reword the step and build again. These steps build reliably:

- **Say the function's word.** "absolute expiration in 15 minutes" and "sliding expiration of 15 minutes" each pick
  one function every time. "for 15 minutes" leaves the choice to the builder.
- **Name the module when two modules could do it.** `[file] check if "work/missing.txt" exists, write to %found%`
  builds as `FileModule.FileExists`. Without `[file]`, the step starts with `if` and becomes a condition.
- **Use the words of the pattern page.** `query file "queries/x.sql", category="home"` with `tables:` and `ds:` lines
  builds as `QuerySqlFile`. "select sql file" builds as reading a text file.

[Writing steps that build](./WritingStepsThatBuild.md) lists wordings checked this way, with the function each one
became.

## See also

- [Writing steps that build](./WritingStepsThatBuild.md)
- [Pitfalls](./Pitfalls.md)
- [Building plang code](./Building.md)
- [Setup and migrations](./SetupAndMigrations.md): the build warns when a setup step that was built before changes
