# Setup and migrations

`Setup.goal` is your schema. It is not a script you edit until it looks right, it is a log of what
has already happened to the databases, and the only safe way to change a schema is to **append**.

Two different run once mechanisms are at work, and knowing which one applies to a file is the
difference between a migration that reaches your users and one that silently does nothing.

## App databases: run once per step

For the datasources your app owns, every setup **step** is remembered by the path of its `.pr` file
in a `SetupRunOnce` dictionary in `.db/system.sqlite`. On startup the runtime walks the setup goal
and skips every step that is already in the dictionary.

So:

- **A new step at the bottom runs once, on the next start.** That is the migration.
- **An existing step never runs again**, no matter what you do to the database afterwards.
- **Editing an existing step is not a migration.** The step is keyed by the `.pr` filename, which is
  derived from the step text, so an edited step usually gets a *new* filename and runs as if it were
  new, while the original stays marked as done. You end up with both, applied in an order you did
  not intend.

## Per user databases: run once per file hash

A datasource created per row, `users/%user.id%` being the usual shape, works differently. Each of
those databases carries a `__Variables__` table with a single `SetupHash` row. When the datasource
is opened, the runtime compares that hash with the hash of the setup goal, and if they differ it
**runs the whole setup file again for that database**, in a transaction, with foreign keys turned
off so a table can be rebuilt.

That is the designed migration path for per user data: append a step, and every user database picks
it up the next time it is opened. There is no list of databases to walk and nothing to schedule.

It also means the create statements in such a file are re-executed on every change, so they have to
tolerate the tables already existing, which they do.

## The mistake to avoid

Removing a column by editing the `create table` statement does nothing, because that statement has
already run everywhere. The database keeps the column and now disagrees with the file that claims to
describe it.

```plang
# wrong, the create statement is history
- create table orders, columns:
    amount(number), bookingId(number)     # deleting this changes nothing

# right, the change is appended
- execute "ALTER TABLE orders DROP COLUMN bookingId"
    ds: "users/%user.id%"
```

Leave the create statement exactly as it ran. There is a second reason beyond honesty: the builder
validates a setup file by building a scratch database **from the create statements in that same
file**, so if you delete the column from the create and then try to drop it, the build fails with
`no such column`. The file has to contain the whole history for the validation to make sense.

## Never touch the database by hand

`sqlite3 app.db "ALTER TABLE ..."` is the one thing that breaks this model for good. The change is
not in the file, so no other environment gets it, and nothing records that it happened. The reverse
is worse: hand applying something that a setup step will later apply leaves that step marked as run
against a database it never touched.

If you need to see what is in there, connect read only.

## While an app is still being built

Before anyone uses the app, none of this ceremony is worth it. You are allowed to rewrite a setup
file as if it had always looked that way, as long as you also clear what says it already ran:

- delete the `SetupRunOnce` entry from `.db/system.sqlite` for the app databases,
- delete the `SetupHash` row from `__Variables__` in each per user database.

Setup then runs from the top on the next start. Do this while the schema is still moving and there
is nothing in the tables worth keeping. The moment somebody real has data, switch to appending and
never look back.

## See also

- [Runtime lifecycle](./RuntimeLifecycle.md)
- [Steps that build the first time](./StepsThatBuild.md)
