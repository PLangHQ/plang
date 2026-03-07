# Learnings — Code Analyzer v1, runtime2-inmemory-datasource

## 1. Trace the full runtime data flow, not just the code in front of you

**What I missed:** SettingsData was registered only on the System actor's MemoryStack (`if (string.Equals(name, "System"...)`). PLang code runs in the User context (`engine.Context => User.Context`). So `%Settings.ApiKey%` would silently resolve to null from any PLang code — the SettingsData object existed, but in the wrong MemoryStack.

**Why I missed it:** I analyzed SettingsData.GetChild in isolation — "does the navigation work? does it hit the database? does it return the right error?" All correct. But I never asked: "when PLang code writes `%Settings.ApiKey%`, which MemoryStack resolves that, and is SettingsData registered there?"

**The fix:** Engine owns a single `SettingsVariable` instance. Every Actor gets it registered via `Context.MemoryStack.Put(engine.SettingsVariable)`. Settings are engine-scoped, not actor-scoped.

**Rule for future reviews:** Pass 4 (behavioral reasoning) must always include a "consumer trace" — start from the PLang code that uses the feature, trace through the runtime to the actual resolution point. Ask: "which actor context does this run in? Is the data reachable from there?"

## 2. Actor context is the #1 data flow trap in PLang

PLang has three actors (User, Service, System), each with their own MemoryStack. But PLang code defaults to User context. Any data registered on a specific actor's MemoryStack is invisible to code running in another actor's context.

**Pattern to watch for:** When reviewing code that registers Data subclasses on MemoryStack (like SettingsData, DynamicData, or future custom Data types), always check: is it registered on ALL actors that need it, or just one? If it's registered conditionally (`if name == "System"`), that's a red flag — trace who actually reads it.

## 3. PASS verdicts need higher scrutiny on behavioral paths

I gave a PASS verdict and focused on OBP compliance, sentinel patterns, and the AfterStep behavior change. The AfterStep finding was valid but lower impact. The SettingsData actor mismatch was the real bug — and I missed it entirely.

**Lesson:** When a feature involves cross-cutting concerns (SettingsData spans Engine → Actor → MemoryStack → PLang variable resolution), the behavioral reasoning pass must trace the FULL cross-cutting path. Mechanical correctness of individual files is necessary but not sufficient.

## 4. Shared vs per-actor state is an OBP design decision

The fix uses `engine.SettingsVariable` — a single shared instance. This is correct because settings are engine-scoped (one database, one truth). The alternative (per-actor SettingsData instances) would create three objects reading from the same database, which is wasteful and could introduce subtle inconsistencies.

**OBP insight:** When Data subclasses override GetChild to intercept navigation, they carry behavioral identity. Sharing the same instance across actors means the behavior is consistent. Deep-cloning would lose the virtual override (MemoryStack.Clone already handles this — `kvp.Value.GetType() != typeof(Data)` preserves subclasses by reference).
