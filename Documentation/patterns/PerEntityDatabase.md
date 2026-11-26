# A database per user or per entity

Most apps keep everything in one database. plang also makes it easy to give each user, or each customer, contract or supplier,
a database of its own: `users/%userId%`, `contracts/%contractId%`. This page is how to do it, and the questions that decide
whether to. It is a choice, not a rule.

## 1. Decide first: ask the developer

Choose it for each kind of data, every time. Ask:

- **Does the data belong to one user or entity, and leave with it?** An export of everything about a person is one file. Deleting
  a person is deleting a folder. When nothing else points into the data, a database of its own fits.
- **How often must you query across all of them?** "Total sales this month", "everyone who bought X", a report over all users:
  each of these opens every database, or reads a copy (section 4). If most questions are across all users, one database fits better.
- **Where is the app heading?** Many small databases are easy to move, back up and delete one by one, and a migration reaches each
  one when it is opened. One database is easier to query, join and back up as a whole.

Data that is about the app, not about one user (products, prices, the identities table, the users table itself) stays in the
main database either way.

## 2. The setup file

Each kind of per entity database has its own setup file in `Setup/`. Its first step names the datasource with a variable:

```plang
/ Setup/SetupUser.goal
SetupUser
- create datasource "users/%userId%"
- create table orders, columns: product(string, not null), amount(number, not null), created(datetime, default now)
```

The main database has its own:

```plang
/ Setup/Setup.goal
Setup
- create table users, columns: name(string, not null), created(datetime, default now)
- create table orderFacts, columns: userId(int, not null), orderId(int, not null), product(string, not null), amount(number, not null), created(datetime, default now)
```

Nothing is created up front. The first step that uses `users/5` creates `.db/users/5/data.sqlite` and runs `SetupUser` on it.

## 3. Using it in a step

Name the datasource in every step that uses it, with `ds:`:

```plang
Start
- insert into users, name="Anna", write to %annaId%
- insert into users, name="Bjorn", write to %bjornId%
- call goal AddOrder userId=%annaId%, product="Book", amount=1200
- call goal AddOrder userId=%annaId%, product="Pen", amount=300
- call goal AddOrder userId=%bjornId%, product="Lamp", amount=4500
- call goal ShowOrders userId=%annaId%
- call goal ShowOrders userId=%bjornId%
- select userId, count(*) as orders, sum(amount) as total from orderFacts group by userId, write to %summary%
- write out %summary%

/ An order goes into the user's own database, and a copy into the shared facts table
AddOrder
- insert into orders, product=%product%, amount=%amount%, ds: "users/%userId%", write to %orderId%
- insert into orderFacts, userId=%userId%, orderId=%orderId%, product=%product%, amount=%amount%

ShowOrders
- select id, product, amount from orders, ds: "users/%userId%", write to %orders%
- write out "user %userId%: %orders%"
```

```
user 1575499857278922752: [{"id":1575499858524631040,"product":"Book","amount":1200},{"id":1575499858835009536,"product":"Pen","amount":300}]
user 1575499857459277824: [{"id":1575499859237662720,"product":"Lamp","amount":4500}]
[{"userId":1575499857278922752,"orders":2,"total":1500.0},{"userId":1575499857459277824,"orders":1,"total":4500.0}]
```

```
.db/data/data.sqlite
.db/users/1575499857278922752/data.sqlite
.db/users/1575499857459277824/data.sqlite
```

A step with no `ds:` uses the main database. Write the variable in `ds:` itself, `ds: "users/%userId%"`, in each step: the
step's own text is what picks the database.

## 4. Data needed across entities: copy it

A report over all users should not open every user's database. Write what reports need into a table in a shared database at
the moment it happens, as `AddOrder` does with `orderFacts`. The summary above is one query on one table, however many users
there are. Decide which facts you need when you design the per entity database: anything not copied can only be found by
opening each database.

## 5. Changing the schema

Append the change to the setup file:

```plang
/ Setup/SetupUser.goal
SetupUser
- create datasource "users/%userId%"
- create table orders, columns: product(string, not null), amount(number, not null), created(datetime, default now)
- add column note(string) to table orders, ds: "users/%userId%"
```

Name the datasource in the appended step too. Without `ds:` the step does not build.

Each per entity database remembers the hash of its setup file. When the file has changed, the whole file runs again on that
database the next time it is opened. Nothing walks the databases: a user who never comes back keeps the old schema until they
do. Reading the existing users after the change above:

```plang
Report
- select id, name from users, write to %users%
- go through %users%, call ShowNotes user=%item%

ShowNotes
- select product, note from orders, ds: "users/%user.id%", write to %orders%
- write out "%user.name%: %orders%"
```

```
Anna: [{"product":"Book","note":null},{"product":"Pen","note":null}]
Bjorn: [{"product":"Lamp","note":null}]
```

The details, and why a setup file is append only: [Setup and migrations](../SetupAndMigrations.md#per-user-databases-run-once-per-file-hash).

## Why this way, and what it costs

- **One database with a `userId` column on every table** is simpler to query and to back up. Choose it when most questions
  are across users.
- **A database per entity** keeps each one's data together and makes export and delete one folder. It costs a copy for every
  fact a report needs, and a backup of many files.

See also: [Setup and migrations](../SetupAndMigrations.md), [App folders](./AppFolders.md).
