# docs v1 — result

## Written
- `docs/modules/llm.md` Configuration: the module's real settings (`%!llm.setting.key|endpoint|model%`, their env fallbacks, the default model `gpt-5.4-nano`, set with `set %!llm.setting.endpoint% = …`); a query with no key (`MissingLlmKey`); the builder's key (`%!llm.decider.setting.key%`, `TYPESAFE_API_KEY`, `%!llm.decider.setting.endpoint%`, `TYPESAFE_ENDPOINT`, asked once and saved, `MissingDeciderKey`).
- `Documentation/v0.2/code-modules.md`: both keys asked the same way (`IKey.Asked`), the decider's settings, `TypeSafe` asks before any request.
- `os/system/builder/builder.code.md` §8: the decider's key.
- `docs/getting-started/installation.md` LLM Setup: a build needs a TypeSafe key, given by `TYPESAFE_API_KEY` or pasted when asked; replaces the payment-prompt text.

## Checked by running (branch built)
- `MissingDeciderKeyTests` 3/3, `MissingKeyTests` 3/3, `DeciderKeyProbeTests` 2/2, `DeciderProviderTests` 2/2.
- First contact, scratch folder, `TYPESAFE_API_KEY` and `OPENAI_API_KEY` unset, stdin `/dev/null`, `plang build --app.setting.create=true`: asks "Building needs a TypeSafe key for the decider. Paste it:", then `MissingDeciderKey (401)` naming `%!llm.decider.setting.key%` and `TYPESAFE_API_KEY`, cause "Channel 'input' has no interactive answerer (stream EOF)", exit 1, no 403, no C# trace. The wording in the docs is the output's.
- Read at source: defaults and env names (`decider.setting.cs:22-38`, `llm/setting/this.cs:19-39`), ask before any request (`TypeSafe.cs:31`), saved on the asker's row (`IKey.cs:38`).

## CHANGELOG
- A build with no TypeSafe key asks once, secretly, and saves it; with no one to ask it stops with `MissingDeciderKey` (401) naming `%!llm.decider.setting.key%` and `TYPESAFE_API_KEY`, instead of the service's 403.

## Open
- Not verifiable here: where a user gets a TypeSafe key, and its cost. The docs no longer promise either (the old "payment prompt" text is gone); `doc/` marks them unverified.
- `docs/getting-started/hello-world.md` still tells a stranger to run `plang exec`, which is no command; `doc/` replaces it, not this branch's change.
