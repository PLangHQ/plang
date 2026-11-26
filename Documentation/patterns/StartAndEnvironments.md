# Start and environments

A web app runs in two places: on your machine while you build it, and on a server with real users. They need different ports,
different settings, different routes, a certificate in one and mocks in the other, and the server must not take a visitor before its
data is in place. The pattern: one `Start.goal` for both, where `live=1` on the command line picks the production branch, seeding runs
before the web server starts, and routes are registered in the web server's `on start` event, split by area.

## 1. Start.goal

```plang
Start
- if %live% == 1 then call SetLiveVars, else SetDevVars
- call goal BeforeStart
- if %live% == 1 then call goal RunScheduledTasks
- start webserver, port: %port%, host: %host%,
    on start call OnStartWebserver
- write out "listening on %host%:%port% (%environment%)"

SetLiveVars
- set default value %port% = 443, %host% = "*", %environment% = "production"
- set environment "production"

SetDevVars
- set default value %port% = 8080, %host% = "*", %environment% = "dev"
- set environment "dev"

BeforeStart
- call goal /import/Countries, on error call SeedFailed
- call goal /import/Currencies, on error call SeedFailed

SeedFailed
- write out to system "Seed failed: %!error.Message%"

RunScheduledTasks
- every 10 minutes, call /job/Cleanup
- start scheduler

OnStartWebserver
- if %environment% == "production" then call SetCertificate
- call goal /routes/Product
- call goal /routes/Admin
- if %environment% == "dev" then
    - call goal /routes/Dev

SetCertificate
- set web certificate "/etc/letsencrypt/live/example.com/fullchain.pem", "/etc/letsencrypt/live/example.com/privkey.pem"
```

```plang
/ routes/Admin.goal
Admin
- add route "/admin/countries", call /admin/Countries
```

```plang
/ routes/Dev.goal
Dev
- add route "/dev/reset", call /dev/Reset
```

Started both ways, with `port=` to move it:

| command | prints | `/product/list` | `/admin/countries` | `/dev/reset` |
|---|---|---|---|---|
| `plang port=8203` | `listening on *:8203 (dev)` | 200, http | 200 | 200 |
| `plang live=1 port=8203` | `listening on *:8203 (production)` | 200, https | 200 | 404 |

- **`live=1`** goes on the production command line only (systemd `ExecStart=plang live=1`). Without it `%live%` is empty and the app
  starts as dev. A `key=value` argument becomes a variable before `Start` runs.
- **`set default value`** fills a variable only when it was not given, so `plang port=8203` wins over the default. A plain
  `set %port% = 8080` overwrites the argument: started with `port=7`, `%port%` is 8080.
- **`set environment`** picks which settings are read: `%Settings.X%` reads `production.X` or `dev.X` ([Settings](../Settings.md)).
  `%environment%` is a plain variable for the goal's own `if`s.
- **BeforeStart** runs before the web server, so the first visitor finds the data there. Each import has its own
  `on error call`: a failing import is written out (`Seed failed: Connection refused ...`) and the next import and the web server
  still start. Without it, one broken import stops the whole start.
- **Imports run on every start**, so each must be safe to run again: `insert or replace` or an update keyed on a stable id, never a
  plain insert.
- **The scheduler starts only under `live=1`**, after seeding. A dev machine does not run the nightly jobs.
- **Routes are registered in `on start`.** A small app adds them all in one `AddRoutes` goal in `Start.goal`. When it grows, split
  them by area into `routes/`: `routes/Product.goal`, `routes/Admin.goal`, and `routes/Dev.goal`, which is called only in dev, so
  dev routes and mocks do not exist in production (404). A route goal never lives inside a folder that a gate guards.
- **The certificate is production only.** Set in `on start`, it is used by the server that is starting.

## 2. What is not visible where

Variables set in `Start` are not there in a request: a route that writes `%environment%` writes nothing. A request starts with its
own variables. Read what a request needs from a setting or the database, not from a variable `Start` set.

## 3. Restart or not

| change | needs |
|---|---|
| a goal's steps changed | `plang build`. The running app uses the new version on the next request |
| a new route, or a changed `add route` | `plang build` and a restart: routes are registered once, in `on start` |
| `Start.goal`, `BeforeStart`, the scheduler | a restart |

## Why this way

- **One Start for both.** Two start files drift apart, and the production one is only tested in production.
- **Seeding in `on start` instead of `BeforeStart`**: the web server is already taking requests, and a slow or failing import there
  meets the first visitors and the scheduler.
- **A value that may be given empty.** `set default value` keeps a variable that was given, even when it was given empty
  (`plang port=` keeps an empty port). When empty must also get the default, write the condition:

  ```plang
  - if %port% is empty then
      - set %port% = 8080
  ```

- **`set default value`, not `load`.** `load %x%, set as 0 if empty` is a different action: it reads a value an earlier run stored in
  the database. Check `.build/Start/SetDevVars/01. ….pr`: `set default value` builds as `SetDefaultValueOnVariables`.

See also: [Recommended app structure](../RecommendedAppStructure.md), [Running on a server](../RunningOnAServer.md),
[Settings](../Settings.md), [Startup parameters](../StartupParameters.md), [Mocking a provider](../TalkingToAnApi.md#mocking-a-provider-for-a-test-instance),
[Calling an outside api](./ExternalApiCalls.md).
