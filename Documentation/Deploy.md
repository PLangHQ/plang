# Deploying a new version

Best practice for putting a new version of your app in front of real users. A new version is a new plang runtime, changed `.goal`
files, or both; the procedure is the same. Start the new version on copies of the production databases, on another port, check it, then
start the real one. Everything is a goal; no shell script is needed. This is level 4 of the
[recommended app structure](./RecommendedAppStructure.md), for web apps that have real users and real data. A small app does not need any of it.

## Before you start

Do not begin until all of these are true:

1. **The app is at level 3:** `live` and `port` as arguments, seeding in `BeforeStart`, the scheduler started only under `live == 1`.
2. **Every outside provider is mocked** and the app starts with `mock=1`: [Mocking a provider](./TalkingToAnApi.md#mocking-a-provider-for-a-test-instance).
3. **`deploy/PrepareTestCopy` and `deploy/Check` exist** and pass against a copy (below).
4. **The test databases are ready:** a copy of every database file and of `.db/system.sqlite`, complete, recent and on a disk with room.
   A copy taken of a database that is being written to is crash consistent, not clean, so take it at a quiet moment or from a snapshot.
5. **There is a way back:** the previous version is still on the machine. A failed runtime update is
   `ln -sfn /opt/plang/<previous> /opt/plang/current` and a restart; a failed goal update is `git reset --hard <previous commit>` in the
   app directory and a restart ([Running on a server](./RunningOnAServer.md)).

## The test instance

1. Copy the production databases and `.db/system.sqlite` to a test folder, once. After the copy it is an independent file.
2. Make the copy safe, once: in the copy folder run `plang exec deploy/PrepareTestCopy`, which sets every provider url and key to a dummy
   ([Mocking a provider](./TalkingToAnApi.md#mocking-a-provider-for-a-test-instance), [Settings](./Settings.md#a-missing-setting-when-no-one-is-there)). It writes to the settings of the folder it runs in, so never run it in the live folder.
3. Start the new version there on another port, with the new runtime if it is changing and the same runtime options production uses
   (for example `--llmservice=`): `plang mock=1 port=8091`. Build the new goals in the copy first.
4. Run `plang exec deploy/Check base=http://localhost:8091`.
5. If it exits 0, start the real instance.

The port is an argument, so `Start.goal` is the same file in both places. `set default value %port% = 443` keeps the
argument when one is given; a plain `set %port% = 443` overwrites it.

What does not run in a test instance: the scheduler (it starts only under `live == 1`; `--no-scheduler` is also available
on newer runtimes), the certificate, and anything else guarded by `live`. Seeding does run, on the copy; that is part of what is tested.

## deploy/Check

```
deploy/PrepareTestCopy.goal  run once in the copy folder: dummy provider settings
deploy/Check.goal            entry: calls the others, throws if anything failed
deploy/CheckDatabases.goal   row and column checks against the production copy
deploy/Expectations.goal     minimum rows the startup must produce
deploy/Smoke.goal            GET the pages that must answer
```

```plang
Check
- set %failures% = []
- call goal CheckDatabases
- call goal Smoke
- if %failures.Count% > 0 then
    - throw error "deploy check failed: %failures%", statusCode 500
```

A goal that ends in `throw error` makes plang exit with 1, so the deploy goal can stop on it. A check that only writes
`FAIL` and ends normally exits 0, and deploys anyway.

- **Smoke:** `- set default value %base% = "http://localhost:%port%"`, never a hard-coded port. A failed page is appended to `%failures%`.
- **Databases:** compare the test copy after startup with the production copy before it. A table must not lose rows or columns.
  Setup that rewrites rows is fine on the copy; compare counts and columns, not contents.
- **Seeds:** `Expectations.goal` lists the minimum rows the startup creates, counted with `select count(*)` on the real database,
  not taken from a log line.
- **Settings:** every `%Settings.X%` used in the goals must exist ([Settings](./Settings.md#a-missing-setting-when-no-one-is-there)).

## Running root commands from a goal

`terminal` runs any program, for example `terminal ln -sfn <target> <link>` or `terminal readlink <link>, write to %target%`.
Do not run plang as root. Give the plang user a sudoers line for exactly the commands needed, then
`- terminal sudo systemctl restart myapp`, and check each step's result before the next.
