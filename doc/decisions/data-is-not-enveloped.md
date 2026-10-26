# Data is not enveloped

**Chosen.** `Data` is the wire shape: `{name, type, value, properties, signature}`. Code does not build a parallel wrapper type ("Envelope", "Wire", "Wrapper") for Data's serialization shape. What should show on the wire or in the store is tagged on the members (`[Out]`, `[Store]`, `[Debug]`) and the writer filters by the view (`CLAUDE.md`, "Data is not enveloped"; `PLang/app/View.cs:7-14`). The historical `Envelope` class was deleted on the branch `data-serialize-cleanup` (`CLAUDE.md`).

**Why, as recorded.** `Documentation/v0.2/obp-smells.md:95` names the smell "envelope": "The field list gets written at every site that packs or unpacks it, and the value is often kept twice (read and raw)." The fix it gives: "store the Data whole and mark what should persist for the view; if a module wraps Data to cache it, it is doing the cache's job."

**Rejected.** A hand-built wrapper "to carry what `[JsonIgnore]` keeps off the wire or out of the store" (`obp-smells.md:95`). A `JsonConverter` for a domain type: `CLAUDE.md` says "do not add a `JsonConverter`"; reason beyond the above not recorded.

**Held by.** Review. The smell's tells are `["Value"]` or `Name == "Value"` outside `app/data/` (`obp-smells.md:95`). `obp-smells.md` keeps one specimen unfixed on purpose: the llm query's own cache (`llm/code/OpenAi.cs`, `RestoreFromCache`).
