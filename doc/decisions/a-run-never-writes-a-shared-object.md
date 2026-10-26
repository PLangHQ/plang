# A run never writes an object its app shares

**Chosen.** A sub app's types, actions and goals are layers over those of the app it is made from, so a shared entry belongs to every app. What a run adds goes to its own layer or its own context, never onto the shared object. A binding made while a program runs is held on the actor that made it. A type takes no history (`Documentation/v0.2/apps.md`, "A run never writes an object its app shares").

**Why.** `apps.md`: "A sub app's types, actions and goals are the origin's, so a shared entry is every app's." On types taking no history: "A read that finds a registered type adds nothing to it; otherwise every request would grow a shared entry for ever." The cost that makes layering worthwhile is recorded too: a sub app costs "about 0.15 ms and 110 KB at birth, against about 19 ms and 2.4 MB for the command line's root app" (`apps.md`, first paragraph).

**What may still write a shared object.** Only a cache that gives every app the same answer: a kinded type made once, a member list made once, a kind coined once (`apps.md`).

**Rejected.** Copying the origin's lists into each sub app: they are "read through and never copied" (`apps.md`). Why copying was rejected beyond that: reason not recorded.

**Held by.** `SharedEntryWritesTests` (1 test) and `HeldBindingTests`. `apps.md` adds: "A write onto a shared object by a run is a bug even when a test passes."
