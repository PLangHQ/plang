# Writing steps that build

One wording for each common task. Each step below was built, its `.pr` file was read (see
[Check the build](./CheckTheBuild.md)), and it ran with the result shown. [Pitfalls](./Pitfalls.md) is the list of
what goes wrong. This page is the list of what to write.

The function after the arrow is what `Function.Name` in the `.pr` file should say. If yours says something else,
reword the step.

## Variables and lists

```plang
- set %greeting% = "Hello"                       → VariableModule.SetStringVariable
- set default value %limit% = 10                 → VariableModule.SetDefaultValueOnVariables
- set %items% = ["a", "b", "c"]                  → VariableModule.SetVariable
- set %names% = []                               → VariableModule.SetJsonObjectVariable
- add "Anna" to list %names%                     → ListDictionaryModule.AddToList
- set %result% = %number% * 2                    → VariableModule.SetVariableWithCalculation
- set %tomorrow% = %Now+1day%                    → VariableModule.SetVariable
```

`set default value` keeps a value that is already there, for example one given on the command line.

## Files

```plang
- write %greeting% to file "work/hello.txt", overwrite             → FileModule.WriteToFile
- append " world" to file "work/hello.txt"                          → FileModule.AppendToFile
- read file "work/hello.txt", write to %text%                       → FileModule.ReadTextFile
- read csv file "work/people.csv", write to %people%                → FileModule.ReadCsvFile
- get files in "work", pattern "*.txt", write to %txtFiles%         → FileModule.GetFilePathsInDirectory
- [file] check if "work/missing.txt" exists, write to %found%       → FileModule.FileExists
```

- `%text%` is `Hello world`. `%people[0].name%` is the first row's `name` column, and `%people.Count%` is the number of rows.
- `%found%` is `True` or `False`.
- Without `[file]`, `if file "x" exists, write to %found%` starts with `if` and is built as a condition. A condition
  cannot write to a variable, so the build stops on that step.
- A path like `work/x.txt` or `/work/x.txt` is inside the app folder.

## Database

The table for these steps:

```plang
Setup
- create table products, columns: name(string, not null), category(string), price(number), updated(datetime)
- create table visits, columns: day(string, not null), count(int, default 0), unique(day)
```

```plang
- insert into products, name="Lamp", category="home", price=1200, updated=%Now%, write to %productId%
                                                     → DbModule.InsertAndSelectIdOfInsertedRow
- insert into products, name="Desk", category="home", price=9900
                                                     → DbModule.Insert
- select name, price from products where id=%productId%, return 1 row, write to %product%
                                                     → DbModule.SelectOneRow     %product.name% is Lamp
- select name from products where id=%productId%, return 1 row, write to %productName%
                                                     → DbModule.SelectOneRow     %productName% is Lamp
- select count(*) as n from products, return 1 row, write to %productCount%
                                                     → DbModule.SelectOneRow     %productCount% is 2
- select id, name from products where category="home" order by name, write to %homeProducts%
                                                     → DbModule.Select           %homeProducts.Count% is 2
- update products set price=%newPrice% where id=%productId%, newPrice=1500
                                                     → DbModule.Update
- update products set category="office" where name="Nothing", dont validate affected rows
                                                     → DbModule.Update           no error when no row matches
- execute sql "update products set price = price + 1 where category = @category", @category="home", write rows affected to %changed%
                                                     → DbModule.Execute          %changed% is 2
- insert into visits, day="2026-11-28", count=1, on conflict(day) do update set count = count + 1
                                                     → DbModule.InsertOrUpdate   run twice, count is 2
- delete from products where id=%productId%
                                                     → DbModule.Delete
- query file "queries/HomeProducts.sql", category="home", write to %fromFile%
    tables: "products"
    ds: "data"
                                                     → DbModule.QuerySqlFile     @category in the file gets "home"
```

- **One column with `return 1 row` gives the value itself.** `%productName%` is the name and `%productCount%` is
  the number. Two or more columns give a row, so you write `%product.name%`.
- An `update` or `delete` that may touch no row needs `dont validate affected rows`. Without it, no row is an error.
- `write to %productId%` on an insert gives the id of the new row.
- For a `.sql` file, write `query file` for a file that reads and `execute` for one that writes, as in
  [SQL in files](./patterns/SqlInFiles.md). "select sql file" is built as reading the file as text.

## Conditions

```plang
- if %email% is empty then                          → ConditionalModule.IsEmpty
    - throw error "Email is missing", status code 400
- if %email% does not contain "@" then              → ConditionalModule.ContainsString, isNot: true
    - throw error "Not an email", status code 400
- if %email% matches "^[^@]+@[^@]+$" then           → ConditionalModule.SimpleCondition
    - write out "looks like an email"
- if %items.Count% > 2 and %email% != "" then       → ConditionalModule.CompoundCondition
    - write out "more than two"
```

The steps that run when the condition holds are indented under it. There is no `else`; write a second `if` with the
opposite test. See [Conditions](./Conditions.md).

## Loops and goals

```plang
- foreach %items%, call ShowItem letter=%item%                → LoopModule.RunLoop
- call goal Double number=21, write to %doubled%              → CallGoalModule.RunGoal   %doubled% is 42
- call goal Slow, dont wait                                   → CallGoalModule.RunGoal

ShowItem
- write out "item %letter%"

Double
- set %result% = %number% * 2
- return %result%

Slow
- wait 1 second                                               → ScheduleModule.Sleep
```

- `foreach` gives the called goal each item under the name you choose, `letter=%item%`. `%position%` is the index.
- A goal started with `dont wait` runs on its own, and what it sets is not seen by the caller.

## Errors

```plang
- read file "work/does-not-exist.txt", write to %nothing%
    on error call FileMissing                                 → FileModule.ReadTextFile
- call goal Validate value=""
    on error call ValidationFailed                            → CallGoalModule.RunGoal

FileMissing
- write out "file was missing: %!error.Message%"

Validate
- if %value% is empty then
    - throw error "Value is required", status code 400       → ThrowErrorModule.Throw

ValidationFailed
- write out "validation: %!error.Message%"
```

- `on error call X` runs X, and the goal then goes on with the next step. `%nothing%` stays empty.
- `%!error.Message%` is the message of the error that X handles.
- To stop after handling the error, end X with `end goal and previous`. To stop the app with a failing exit code,
  end it with `end app, exit code 1`.

## Cache

```plang
- set cache "fixed" = "a", absolute expiration in 15 minutes     → CachingModule.SetForAbsoluteExpiration  900
- set cache "renewed" = "b", sliding expiration of 15 minutes    → CachingModule.SetForSlidingExpiration   900
- get cache "fixed", write to %fixed%                            → CachingModule.Get
- remove cache "fixed"                                           → CachingModule.RemoveCache
```

Write "absolute expiration in" or "sliding expiration of". A plain "for 15 minutes" does not say which, and the
builder may choose either from one goal to the next.

## See also

- [Check the build](./CheckTheBuild.md)
- [Pitfalls](./Pitfalls.md)
- [Writing goals](./WritingGoals.md)
- [Patterns](./patterns/README.md)
