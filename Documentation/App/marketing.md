# PLang Marketing & Documentation Strategy

## Core Pitch

PLang isn't a programming language. It's an engine. The language is just how you talk to it. You don't build infrastructure — you build your app. Everything else is free.

---

## Lead With Examples, Not Explanations

The examples sell themselves. Don't explain what PLang is — show what it does.

### "You didn't set anything up"
The sharpest hook. A developer reading `cache.set %key% %value%` will immediately ask "where's the config? where's the DI? where's the Redis connection string?" The answer is: there isn't one. It just works. That's the moment they get it.

### "A website in 10 lines"
Start, routes, render. No framework, no config. The power isn't that it's short — it's that a business person can read it and understand what their backend does.

### "Search in plain English"
`%engine.Products% that fits with %query%` — not SQL, not an API, just a question. It doesn't look like code. It looks like a sentence someone typed. That's the point.

### "A CMS in 20 lines"
Webserver + database + cache + templates. Everything a CMS needs, nothing it doesn't.

---

## Three Audiences, Three Messages

### Non-programmers: "You can read it, so you can write it"
Show the .goal file. That's it. "This is your app. You can read it. Change a word, the app changes."

### Developers: "Embeddable runtime with batteries included"
Show the Engine object graph. Show `engine.Libraries.Add`. Show that it's embeddable, extensible, typed. Cache, IO, events, LLM, file system, serialization — all there. They'll see it's real software, not a toy. "You just add your domain logic."

### Businesses: "Your entire backend in files you can read in 5 minutes"
Show before/after. "Here's what your team built in 3 months. Here's the same thing in PLang. You can read it yourself."

---

## Engine as Platform — The Developer Story

The engine is a self-contained root object. Everything hangs off it. You don't configure it, you use it.

- **Domain data on the engine**: `engine.Products = %products%` — your domain objects live alongside cache, IO, events, file system. The engine is both infrastructure AND your data host.
- **Semantic querying for free**: `%engine.Products% that fits with %query%` — the builder already sends natural language to the LLM. Products in memory, query is a variable, LLM resolves it.
- **Engine-level callable properties**: `engine.Summary = call goal GetSummary %product%` — goals become callable properties. GoalCall as a value type.
- **Self-hosting**: PLang code manipulates its own engine — add events, load goals, run goals. PLang embedding PLang.
- **External libraries**: `engine.Libraries.Add("astrolib.dll")` — load any .dll, its actions become PLang steps.
