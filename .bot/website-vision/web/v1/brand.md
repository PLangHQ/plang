# PLang — Brand Foundation (draft v1)

## Vision
**Intent is the new source code.**

## Belief
For seventy years, programming has meant translating what you mean into what a machine accepts.
The translation was the job: syntax, boilerplate, glue.
Now machines understand intent. The translation can be compiled, so it no longer has to be written.
What remains is the part that was always yours: knowing what you want.

AI coding tools got this half right. They generate code faster, but you still own a codebase you didn't write and can't fully read.
PLang keeps the intent as the source. The code is build output, like a binary.

## Promise
**Write what you mean. Keep what you wrote.**
- You write goals in plain language.
- PLang compiles them once, at build time.
- What runs is deterministic, readable and yours. No AI is required at runtime.

## Proof pillars (B: read, own, trust)

| # | Pillar | Claim | Proof on the page | Status |
|---|---|---|---|---|
| 1 | **Readable** | The source is the intent. | A goal file, full size, readable by anyone | ✅ |
| 2 | **Compiled** | AI at build time. Not at runtime. | Each step → its compiled `.pr` action (module, action, parameters), side by side | ✅ `Tests/Http/GetRequest/.build/` |
| 3 | **Transparent** | Every step, open. | The `.pr` JSON is plain and versionable. View source on everything. | ✅ |
| 4 | **Complete** | Batteries included. | A module grid: http, db, file, llm, crypto, identity, signing, ui, timer, cache… (~35) | ✅ `os/system/modules/` |
| 5 | **Signed I/O** | Everything in and out is signed. | All I/O is signed by default (`unsigned` is the opt-out). Every actor has a keypair. | ✅ confirmed by Ingi |
| 6 | **Contained** | Apps can't reach outside their root. | Filesystem permission gate on every file access | ✅ (AuthGate) |

Example of pillar 2, taken from the repo:

```
get https://httpbin.org/get, unsigned, write to %response%
```
↓ build
```
http.request   Url="https://httpbin.org/get"  Method=GET  Unsigned=true
variable.set   Name=%response%  Value=%!data%
```

## Voice
- **Short, declarative, calm.** Facts stated plainly; no exclamation marks, no "revolutionary".
- **Show before tell.** Every claim sits within one scroll of its proof.
- **Honest about cost and stage.** If something is early, say so in one quiet line.
- Banned words: revolutionary, game-changing, magic (as a claim), effortless, 10x, WTF.

Headline register:
> Write what you mean.
> AI at build time. Not at runtime.
> Every step, open.
> Yours. Signed.

## Look
- **Space is the luxury.** One idea per screen.
- **Type:** the system stack (SF Pro on Apple devices), big and tight for headlines, plus one monospace face for code.
- **Colour:** near-white/near-black, one grey scale, one accent from the puffin's beak orange.
- **The goal file is the hero image.** Treat code the way Apple treats product photography: large, centred, lit.
- **The puffin is the signature, not wallpaper.** Logo, one appearance in the hero, footer. Same artwork, used sparingly.
- **Motion has one job:** showing intent becoming compiled steps.

## Release
The site sells **PLang 1.0b** (runtime2). Install is a script: `.ps1` on Windows, bash on macOS/Linux.

Install state per the os bot (2026-10-13): the only script is PlangOS's Windows launcher (`irm https://plang.is/start.ps1 | iex`). It is unpublished (version 0.0.0) and installs **PlangOS** (a WSL desktop), not plain PLang. No bash installer exists. Today's build uses OpenAI (`OPENAI_API_KEY`); "llm.plang.is, no key" is a later idea. The design uses placeholders until Ingi decides.

## Open
1. Which installer ships with 1.0b: a plain PLang runtime installer, or PlangOS? Plus the Windows and macOS/Linux one-liners.
2. Build-time LLM for 1.0b: own OpenAI key, credits, or llm.plang.is?
3. A higher-resolution puffin, plus Ingi's favourite examples.
