# decider-key

Cut from runtime2 493b157df (json-lines' merge). One stage, tiny on purpose (Ingi, 2026-10-25: "yes", before `windows`).

## Why

A stranger's first build with no TypeSafe key ends in an error that doesn't say what's wrong (doc2, `doc/when-a-step-fails.md`):

```
plang build
```

**Before:** `HttpError 403` from api.typesafe.ai, with a trace. Nothing says a key is missing, which key, or how to give it.

**After:** plang asks once, secretly: "Building needs a TypeSafe key for the decider. Paste it:". It saves the answer on the actor's row and never asks again. With no one to ask (no input, or an empty answer), it stops with `MissingDeciderKey`, naming both ways to give a key: `%!llm.decider.setting.key%` and the `TYPESAFE_API_KEY` environment variable.

That's what the OpenAI key already does (`module/llm/setting/this.cs`, `Asked`; `code/Live.cs:20–22`; `MissingLlmKey`).

## The stage

- The decider's setting (`module/llm/decider.setting.cs`) answers its key as asked, the way llm's setting does.
- TypeSafe (`module/llm/code/TypeSafe.cs:28–41`) takes its key from that answer before any request goes out. A missing key never reaches the service, and no request goes out without one.
- **One door:** the two keys ask the same way, so the asking is written once, not copied into the second setting. Where that one place lives is the coder's shape to bring (both settings are the llm module's; one is a module setting, the other a plain setting).
- **Out:** a wrong key (the service refusing one that was given). Its answer is the service's own error. Making that readable too is a later step, if doc2 shows it's needed.

## Tests (C#, each red first)

Beside `MissingKeyTests` and `DeciderSettingTests`:
- A missing decider key is asked once, saved, and never shown; the second build doesn't ask.
- With no one to ask, the build fails with `MissingDeciderKey` naming both ways, and no request reaches the service.
- A saved key, or one from the environment, is used without asking.
- The OpenAI key's tests stay as they are (the shared asking moves nothing for them).

No locked surface: settings classes and the llm module's code are the module's own.
