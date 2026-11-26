# Mocking providers

A test instance must never reach a real payment provider, shipping service or customer mailbox. Part of the
level 4 of the [recommended app structure](./RecommendedAppStructure.md), for web apps that have real users and real data; the test
instance itself is in [Deploying a new version](./Deploy.md). A small app does not need it.

## The switch

`plang mock=1` starts a mock instance. It is a separate argument from `live=1` and the two must never be combined:

```plang
Start
- if %live% == 1 then call SetLiveVars, else SetDevVars
- if %mock% == 1 then call goal StartMock
- call goal BeforeStart
...

StartMock
- if %live% == 1 then
    - throw error "live=1 and mock=1 together are not allowed", statusCode 500
- set environment "production"
- set %environment% = "production"
- call goal /dev/mocks/MocksSetup
```

The same name in every app. A mock instance starts like a development instance, but with the production settings (the environment is
`production`), otherwise it proves nothing about production. The dummy settings are not written here: they are written once, when the
test copy is set up (see below), so no start can ever overwrite settings.

## What to mock

Mock what moves money, reaches a real person or books something physical: payment providers, shipping, mail to customers, accounting.
Cheap, read-only calls can stay real. An AI provider may stay real; remember that a copy of production holds real customer data.

## One registry

`dev/mocks/MockApis.goal`, with one goal per provider:

```plang
MockApis
- call goal MockPayments
- mock http PostMultipartFormData url:https://api.example.com/v3/%Settings.MailDomain%/messages, call MockMail
```

`mock http` is bound to the request context. Registered at startup it never applies to a request, so register it from
`OnGoalRequestBegin` behind a cache flag that `MocksSetup` sets.

## A mock fails open

A mock matches a module and a method name, and a `*` only at the start or end of a value (`https://api.example.com/*`). When the step
changes (`post` becomes `post multipart form data`), or the value has a `*` in the middle, the mock silently stops matching and the call
goes to the real service. Keep the match simple: one value, with `*` at its end. Two defences:

1. **Dead values, set once.** When the test copy is created, a goal overwrites every provider url in the COPIED `system.sqlite` with a
   dead address and every key with a dummy. It runs once, never at start. The mocks still match, because they read the same settings:
   ```plang
   PrepareTestCopy
   - set environment "production"
   - set %Settings.PaymentUrl% = "http://127.0.0.1:9/dead"
   - set %Settings.PaymentApiKey% = "test"
   ```
   Run it in the copy folder: `plang exec deploy/PrepareTestCopy`. A missed mock now fails with "connection refused" instead of
   charging a card. `set environment "production"` comes first so the settings land in the `production.` rows.

   **These steps write to the settings of the database in the folder where they run.** Run them only in the folder that holds the copy,
   never in the live folder: there they would replace the real urls and keys.
2. **Verify the mock fired**, not that the page looked right. Each mock records a hit, and the deploy check asserts that the
   expected providers were hit.

A url written inside a goal (not read from settings) cannot be redirected by a setting. Mock it in the registry, and use the
provider's own test key where one exists.

## See also

[Talking to an API](../TalkingToAnApi.md) for the mock rules, [Settings](./Settings.md), [Deploying a new version](./Deploy.md).
