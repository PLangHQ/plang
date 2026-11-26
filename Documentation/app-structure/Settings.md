# Settings

`%Settings.X%` reads a row from `.db/system.sqlite`. Relevant from level 3 of the [recommended app structure](./RecommendedAppStructure.md) (an app with environments and real keys).

## A missing setting asks, and stores an empty value

Reading a setting that has no row makes plang ask for it. In a run without a person (a server, a test, stdin closed) the answer is
empty: the step continues with an empty value, the run does not fail, and an empty row is stored from then on, so the setting looks
"set" afterwards. A later real value has to overwrite it.

So never rely on a setting being absent:

- A test instance sets a dummy for **every** `%Settings.X%` the app reads ([Mocking](./Mocking.md), `PrepareTestCopy`).
- The deploy check compares the `%Settings.X%` names used in the goals with `select Key from Settings` and fails by name for any
  that is missing ([Deploying a new version](./Deploy.md)).

## The environment is part of the key

`set environment "production"` makes `Settings.X` read the row `production.X`; `dev` reads `dev.X`. A copy of the production
`system.sqlite` has `production.` rows only, so a test instance that runs as `dev` finds none of them. Run the test instance with the
same environment as production.

## Two different rows can share a name

`%Settings.OpenAiKey%` is a row owned by the settings class. The runtime's own LLM service reads its own row, owned by the service,
under the same key name. Setting one does not change the other. If you need to block or replace the key for `[llm]` steps, change the
service's row, not only `%Settings.OpenAiKey%`.

## Where the setup ledger lives

The list of setup steps that have already run is a row in the same file (`<environment>.SetupRunOnce`, `production.SetupRunOnce` by default). A copy of `system.sqlite` carries it,
so a test copy does not repeat setup that production already ran. See [Setup and migrations](../SetupAndMigrations.md).
