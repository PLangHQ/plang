## Replaced decisions (read before any row below)

| replaced | by | what changed |
|---|---|---|
| — | — | — |

| # | what | rule | detail | who | waits on |
|---|---|---|---|---|---|
| 2 | **Stage 1 done: made on the first ask** (plang-coder 4dcfa3f36: the step cache a `Lazy<ICache>`; a frame's store, Tags and Errors made on first write, with reads that find none making nothing; `goal.Broken()` judged at the first start and kept. Pins: a sub app's birth 86,036 → 39,008 B (−55%), a frame with no write 2,592 → 1,544, an empty goal's restart 4,376 → 4,136. C# and plang green) | 11 lazy; bench rows are the codeanalyzer's (Ingi, 2026-10-25) | The codeanalyzer takes the bench rows for stage 1's hash. Stage 2 to the coder: `Kinded`'s shape comes first, since it's a new member on the locked type system and goes to Ingi. | codeanalyzer; coder | the rows; `Kinded`'s shape |
| 1 | **call-cost is cut; five stages** (Ingi, 2026-10-29/30: "on the questions I agree new branch", "1 yes on 4 and 5", "agree", "Ok"; ReadyToRun "no not yet") | 11 lazy; 5 a birth fact; jump between references; no static | From runtime2 7bf093245. Stages: 1 made on first ask (the step cache, a frame's store, `Broken()`); 2 a type found, not made (number's operator births; the type readers; `type.Kinded(kind)` NEW); 3 a lookup kept by its owner (the shortcut's name, a goal index, the setting store's answer); 4 the generator (one handler per run, emitted binding; its shape confirmed first); 5 a copy shares its bag until written (measured first). Each stage is measured before and after and pinned in bytes. Plan: `plan/start.md`. | coder; codeanalyzer | stage 1 |
