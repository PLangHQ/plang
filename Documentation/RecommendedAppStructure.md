# Recommended app structure

What an app needs depends on what it does. Start with the smallest level that fits and add the next one when the app needs it.
Each level only adds to the one before; nothing here has to be built in advance.

| Your app | Level | You need |
|---|---|---|
| Runs a task and ends (console, script, tool) | 1 | `Start.goal`, `Setup/` if it has a database |
| A web app you run yourself, small, one machine | 2 | level 1 + `start webserver` with a port |
| A web app with real users on a server | 3 | level 2 + `live`, environment, seeding before the first visitor, scheduler, certificate |
| A web app you update over real data | 4 | level 3 + the procedure in [Deploying a new version](./Deploy.md) |

## Level 1: console app

```plang
Start
- set default value %name% = "world"
- write out "hello %name%"
```

Run with `plang` or `plang name=Anna`. Arguments of the form `key=value` become variables; `set default value` keeps the one given and
fills in the one that is missing. A goal other than Start runs with `plang exec path/to/Goal key=value`, and a goal that ends in
`throw error` makes plang exit with 1, so a script can be used from the shell.

If the app has a database, the schema goes in `Setup/` and is append only: [Setup and migrations](./SetupAndMigrations.md).
There is no `live`, no port and no mock at this level.

## Level 2: small web app

```plang
Start
- set default value %port% = 8080
- start webserver, port: %port%
```

The port is an argument with a default, so `plang port=8091` starts it elsewhere without touching the file. Always `set default value`,
never a plain `set %port% = 8080`, which would overwrite the argument.
Start the web server in `Start`. It can also be started from a goal that runs in a request or on the scheduler (a second web server
started by a deploy, for example).
Routes, pages and the request gate: [Running on a server](./RunningOnAServer.md), [Identity in the browser](./IdentityInTheBrowser.md),
[Events](./Events.md).

## Level 3: web app in production

Add the three things production needs that development does not: a different environment, seeding that finishes before the first
visitor, and the scheduler.

```plang
Start
- if %live% == 1 then call SetLiveVars, else SetDevVars
- call goal BeforeStart
- if %live% == 1 then call goal RunScheduledTasks
- start webserver, port: %port%, host: %host%,
    on start call OnStartWebserver
    on goal begin request call OnGoalRequestBegin

SetLiveVars
- set default value %port% = 443, %host% = "*", %environment% = "production"
- set environment "production"

SetDevVars
- set default value %port% = 8080, %host% = "*", %environment% = "dev"
- set environment "dev"
```

- `live=1` on the production command line (systemd `ExecStart=plang live=1`).
- Work that must finish before the first visitor (imports, seeding) goes in `BeforeStart`, with `on error call` on each import so one
  broken import cannot take the site down. The `on start` event is for what the web server itself needs, such as routes and the certificate.
- The scheduler starts only under `live == 1` and after seeding.
- The certificate is production only.
- Every setting is read for the current environment (`production.X` or `dev.X`): [Settings](./Settings.md).

### What BeforeStart does

It prepares the data the app needs before it takes traffic: importing reference data, seeding tables, anything a page would
otherwise find missing. It runs on every start, so every step must be safe to run again (an upsert, not a plain insert).

```plang
BeforeStart
- call goal /import/Countries, on error call SeedFailed
- call goal /import/Translations, on error call SeedFailed
- call goal /import/EmailTemplates, on error call SeedFailed

SeedFailed
- write out to system "Seed failed: %!error.Message%"
```

- **One `on error call` per import.** `on error call X` hands the error to X and the goal carries on with the next step, unless X itself
  throws. A broken import is logged and the site still starts. Without it, one failing import stops the whole start.
- **Imports are idempotent**, because they run on every start: `insert or replace` or an update keyed on a stable id, never a plain insert.
- **Not here:** anything that needs the web server running (routes, the certificate), which belongs in the `on start` event, and
  anything that must run repeatedly, which belongs to the scheduler.
- Why it is a goal of its own and not the `on start` event: the scheduler and the first requests must not start before the data is
  there. A failing import inside the `on start` event, with the scheduler already running, can leave the site unresponsive.

## Level 4: updating a web app that has real data

Deploying a new version of an app that has real users and real data, whether it is a new plang runtime, changed goals or both, is a
procedure of its own: the app has to be set up for it, the test databases have to be ready, and there are several steps in a fixed
order. It is only worth the work once a deploy can hurt real users, and a small app does not need it.

The full best practice is on its own page: [Deploying a new version](./Deploy.md). It builds on [Mocking a provider](./TalkingToAnApi.md#mocking-a-provider-for-a-test-instance) and
[Settings](./Settings.md).

## Other topics

| Topic | Page |
|---|---|
| Schema and migrations | [Setup and migrations](./SetupAndMigrations.md) |
| Authorization: who may see which page | [Identity in the browser](./IdentityInTheBrowser.md) |
| Identity | [Identity](./Identity.md) |
| Events | [Events](./Events.md) |
| Errors | [Error handling](./ErrorHandler.md) |
| Running on a server | [Running on a server](./RunningOnAServer.md) |
| Startup parameters | [Startup parameters](./StartupParameters.md) |
| External APIs and their mocks | [Talking to an API](./TalkingToAnApi.md) |
