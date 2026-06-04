# Baseline — v10 (Stage 10: value-conversion onto types)

Captured before any changes, on branch `type-kind-strict` at the post-pull HEAD.

## C# — `dotnet run --project PLang.Tests`
- total: **3822**, failed: **0**, skipped: 0 — all green.

## PLang — `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`
- **262 total, 259 pass, 3 fail, 0 timeout, 0 stale, 0 skipped**
- Pre-existing failures (NOT mine — event/Trigger merge):
  - `/Channels/Events/AddOnAsk/Start.test.goal`
  - `/Channels/Events/AddBeforeWrite/Start.test.goal`
  - `/Modules/Event/Basic/Events.test.goal`

Regression rule: any of the 259 green PLang goals or 3822 C# tests going red is mine.
The 3 event failures are the reference floor.
