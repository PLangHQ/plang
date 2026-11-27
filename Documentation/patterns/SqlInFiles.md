# SQL in files

A short query belongs in the step. A long one, a report with a `with` clause, several joins and computed columns, is easier to
write, read and test as SQL on its own. The pattern: keep short SQL inline, put long SQL in a `.sql` file in a `sql/` folder and run it
with `query file`.

## 1. Short SQL inline, with `\%` for a literal %

In a step, `%` starts a variable. A literal `%` is written `\%`:

```plang
- select name from products where name like '\%apple\%' order by name, write to %b%
- select name from products where name like 'Red\%' order by name, write to %d%
- select name from products where name like \%%q%\% order by name, write to %c%
```

| step | answer |
|---|---|
| `like '\%apple\%'` | `[{"name":"Apple juice"},{"name":"Red apple"}]` |
| `like 'Red\%'` | `[{"name":"Red apple"}]` |
| `like \%%q%\%`, `%q%` = apple | `[{"name":"Apple juice"},{"name":"Red apple"}]` |
| `like '%apple%'` | `[]` |

The last one builds and runs without an error and finds nothing: `%apple%` is read as a variable named `apple`, which is empty.
`like \%%q%\%` builds as `like '%' || @q || '%'`, so the value is a parameter.

## 2. Long SQL in a file

```sql
-- sql/categories.sql
-- products per category, with the share of products priced at or above the limit
with priced as (
    select category,
           count(*) as products,
           sum(case when price >= @minPrice then 1 else 0 end) as aboveLimit,
           avg(price) as avgPrice
    from products
    group by category
)
select category,
       products,
       aboveLimit,
       round(100.0 * aboveLimit / products, 1) as pctAbove,
       round(avgPrice, 2) as avgPrice
from priced
order by products desc
```

```plang
- query file "sql/categories.sql", minPrice=9, write to %f%
    tables: "products"
    ds: "data"
```

```json
[{"category":"fruit","products":3,"aboveLimit":2,"pctAbove":66.7,"avgPrice":10.0},
 {"category":"drink","products":1,"aboveLimit":1,"pctAbove":100.0,"avgPrice":30.0}]
```

- A parameter is `@name` in the file and `name=value` in the step. The name left of `=` is the parameter, not the variable:
  `cancellationInfo=%cCancellation%` binds `@cancellationInfo`. The build reads the file and checks that every `@name` in it gets
  a value from the step; one that gets none stops the build with `save.sql uses @extra, and the step gives no value for it`.
- A file that writes runs with `execute`, and takes its values the same way:
  `execute "sql/mark.sql", email=%email%` for `update subs set marked = 1 where email = @email`. The same build check applies.
- `%` in the file is plain SQL: `where name like '%' || @q || '%'` needs no escaping.
- **Write `tables:` and `ds:`.** Without them the step can build without its datasource and table list, and fails when it runs with
  `Could not find parameter dataSourceNames`. The two lines make the build include them. Check the `.pr`: `dataSourceNames` and
  `tableAllowList` must be among the parameters.
- **The file is read when the step runs.** A change to the `.sql` file is used on the next run without building the step again.
  A new `@name` in the file needs a `name=value` in the step, and the step must be built again so the parameter is in the `.pr`.

## Why this way

- **Long SQL inline** is read by the builder and can be rewritten by it, every `%` in it has to be escaped, and it cannot be pasted
  into a SQL tool to test.
- **Short SQL in a file** hides a one-line query from the goal that runs it. One file per short query is more to look for than to read.
- Which is long is a judgement: a query that does not fit on two or three lines of the step, or that you would want to run in a SQL
  tool while writing it, goes in a file.

See also: [Cache keys](./CacheKeys.md), [Setup and migrations](../SetupAndMigrations.md), [Pitfalls](../Pitfalls.md).
