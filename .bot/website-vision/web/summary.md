# web — summary

**Version:** v1 (in progress, waiting on Ingi's review of the words)

**What this is:** A redesign of plang.is, done the way an agency would: vision → belief → promise → proof → voice/look → site. The aim is an Apple-like, simple, factual site. The puffin artwork stays as is.

**What was done:**
- Ingi chose vision C, "Intent is the new source code."
- Wrote the brand foundation in `.bot/website-vision/web/v1/brand.md`: belief, promise ("Write what you mean. Keep what you wrote."), six proof pillars with sources in the repo, voice rules, and visual principles.
- The current plang.is content was used for information only. It describes v0.1.

**Next:** Ingi reviews brand.md. Then the one-page narrative, then 2–3 hero prototypes, then the full single-file page.

**Open:** v0.2 facts (LLM providers, how builds are paid for, install command), whether http signs by default, a higher-res puffin, Ingi's favourite examples.

**Code example (proof pillar 2):**
`get https://httpbin.org/get, unsigned, write to %response%` → `http.request Url=… Method=GET Unsigned=true` + `variable.set Name=%response%`
