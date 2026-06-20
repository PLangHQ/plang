# Why I'm building PLang

I've been programming for over twenty years. I'm good at it. But I kept running into the same wall: the person with the idea can't build the thing.

Not because they're not smart. Because programming languages were designed for computers, not for people.

---

## The gap nobody talks about

Every framework, every no-code tool, every AI coding assistant tries to patch the same problem: programming is hard to learn and even harder to read back. You write something, come back in six months, and spend half a day figuring out what you meant.

I wanted something different. Not a tool that hides the code — that just moves the wall. I wanted a language where the code *is* the plain description of what you want to happen.

That's PLang.

```plang
FileProcessor
- read file 'orders.csv'
- foreach %orders%, call ProcessOrder
- write 'done' to output
```

Anyone can read that. The person who asked for the feature, the person who will maintain it in three years, the new hire on day one.

---

## What I gave up

PLang is not fast. It's not for writing operating systems or game engines. The LLM step in the builder adds latency that C# developers would never tolerate.

I decided that was fine. The programs that matter to most people — automations, scripts, integrations, internal tools — don't need microsecond latency. They need to *exist* and be *maintainable*.

---

## Where it's going

I'm building this from Reykjavík, mostly alone, mostly at odd hours. It's pre-1.0 and it shows. There are rough edges. The type system is being redesigned. The builder is non-deterministic in ways that drive me slightly mad.

But the idea is sound. Programming in plain English — not as a gimmick, not as a layer on top of "real" code — as the actual model.

I think that matters.

— Ingi
