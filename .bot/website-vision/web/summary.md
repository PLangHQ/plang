# web — summary

**Version:** v2 (draft homepage, waiting on Ingi's review)

**What this is:** A redesign of plang.is, done the way an agency would: vision first, then proof, then the page. PLang is presented as a **platform, a new internet**, not as a programming language. Regular users describe what they want, the LLM writes PLang, and they never see the language.

**What was done:**
- v1: brand foundation around "Intent is the new source code", plus three hero prototypes. Ingi rejected them: too programming-focused, and nothing special. He wants a white background, not black.
- v2: vision **"An internet made by the people who use it."** and promise **"Say what you need. Own what you get."** (`v2/brand.md`).
- Full draft homepage at `v2/site/index.html` (+ `puffin.png`). Sections: Hero (a person's request becomes an app, with 3 rotating examples) → It's yours → No passwords → Everything is signed → Open all the way down → For builders → Start → Footer.
- Rules: never mention PlangOS. The language appears only as proof (Open, Builders). White background, dark mode in dark grey.

**Next:** Ingi's review of the draft. Then copy polish, a real example goal verified on 1.0b, a hi-res puffin, and hosting.

**Open:** The example apps (Club Times, Invoice Digest, Bake Sale) and the LogRun.goal are illustrative and not built. "Monthly fee: None" needs confirming against llm.plang.is pricing. macOS/Linux install is not available yet.

**Code example (hero data shape):**
`{ who: "Anna, running club", said: "Let our running club log times…", name: "Club Times", foot: ["On Anna's computer","4 members","Signed"] }`
