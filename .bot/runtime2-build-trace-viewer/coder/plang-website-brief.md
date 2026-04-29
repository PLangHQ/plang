# plang.is — Design Brief for Claude Designer

**Audience:** you, the designer.
**Author:** coder (relaying Ingi's design direction).
**Status:** go wild. The constraints here are aesthetic and philosophical, not pixel-exact. Bring a vision.

---

## Read this first: the one idea that controls everything

PLang is a programming language where a single line does what 50 lines of Python does. Example:

```
set %!app.debug.enable% = true
```

That line puts the entire application into debug mode. No config file. No middleware wiring. No feature flag framework. A variable assignment with a prefixed name — and a deep capability flips on.

**That is the thesis of the language, and it must be the thesis of the website.**

The current site doesn't get this across. It's structured like a C# README — navigation, pitch, feature grid, examples, pricing, community. That shape is fine for a language that needs to *justify* itself. PLang shouldn't justify. It should show one line, and then another, until the reader realizes something uncomfortable is happening. That's the pitch. The pitch is the shock.

---

## What the current site gets wrong (diagnostic, not cruel)

1. **Opens with a claim instead of a demonstration.** "Next evolution in programming languages" asks the reader to trust. A line of code that does something impossible asks them to *notice*.
2. **Feature grid.** Database, messaging, auth, UI, scraping, caching — shown as tiles. This flattens PLang back into the shape of every other language's homepage. The grid is the shape of "we have parity." PLang doesn't have parity. It has one-line expressiveness. Parity is not the sell.
3. **Screenshots of app windows.** A window showing a CRUD table proves PLang can build boring-looking things. That's not interesting. The *code that produced it* is interesting.
4. **Density without hierarchy.** The eye has nowhere to land. Simplicity is PLang's product; the website cannot be visually dense.
5. **Marketing voice creeping in.** "You have never seen software being built this fast." That's a startup-page sentence. PLang is closer to an artist's manifesto than a SaaS product. The tone should match.
6. **CTAs are backwards.** "Download" is a button at the top. The better CTA is a line of PLang code that *is* the download. Read on.

---

## The new direction, in one sentence

> The website is a parade of one-liners. Nothing else. Prose is rationed. Features are not grids — they are vignettes. Typography is the main visual element. The site behaves the way the language behaves: inexplicably short, and more capable than it looks.

---

## Content structure (hero → footer)

### 1. Hero — one line, nothing else

No logo-wall navigation. No tagline beneath a title. No "[Download] [Docs] [Get Started]" cluster.

Just:

```
- every morning at 7am, send weather to %me.phone%
```

Centered. Monospace. Huge. A quiet line below in small text: `that's the whole program.`

Scroll indicator. That's it. The reader has already seen more than a feature grid would ever have told them.

### 2. The parade (eight to twelve one-liners, one per screen)

Each screen is a single line of PLang, with a tiny caption underneath revealing what it does. No images. No icons. Just the line, and the revelation. The rhythm is the product.

Example lineup:

```
- set %!app.debug.enable% = true
  entire app runs in debug mode
```

```
- on error retry 3 times, call ChargeCard
  automatic retries, no framework
```

```
- every 5 minutes, call CheckInbox
  scheduling, no cron
```

```
- select * from users where signup > %yesterday%, cache for 10 min
  database + cache in one step
```

```
- foreach %products%, call PriceIt item=%product%
  parallel fan-out over a list
```

```
- call airline.Book, on rollback call airline.Cancel
  distributed transaction, language-level
```

```
- backup %!db% to s3://backups/
  one step, one backup, done
```

The design question: how to pace this. I'd suggest: one line fills the viewport, scroll reveals the next, previous fades slightly. No animation tricks — just presence. You are reading a book, not watching a reel.

### 3. The thesis — prose for the first time, kept short

After the parade, the reader is primed to hear *why*. One section, maybe 150 words. Written in plain voice, not marketing voice. Something like:

> PLang is written in natural language. A large language model compiles your intent into executable steps. The language isn't English with code sprinkled in — it's a formal language whose surface happens to read like English. A line like `select users where signup > %yesterday%, cache 10 min` isn't a translation; it's the program. The runtime knows the difference.
>
> Version 0.1. Each line of code costs between $0.005 and $0.035 to compile, one time. After that, it runs forever.

The last sentence (the cost) is load-bearing. It tells the reader you are honest. Honesty is rare on SaaS pages and rarer on language pages. Keep it.

### 4. The comparison — one at a time

Pick one scenario. Show PLang. Show Python. Collapse the Python by default.

```
PLang:
- every 5 minutes, fetch %url%, if status != 200, email %me%

Python: (click to expand)
```

The collapsed Python is the sell. Readers don't expand it — they just know it would've been long. You're showing restraint on their behalf.

Do this *once*, not ten times. Ten comparisons = feature grid by another name.

### 5. Try it — browser REPL, not a screenshot

An input box. User types a line of PLang. It runs. The output appears. No signup. No modal. The first time a reader types `- write out "hello"` and it writes back `hello`, the website has won.

If a full REPL is too much for v1 of the site, ship a *scripted* REPL — a pre-filled example that runs step by step on a button press. Second best, but still better than any screenshot.

### 6. The philosophy — why simpler matters

A long-form read section. Not required on the homepage, but linked. This is where Ingi's voice lives — why he built this, what he gave up, what he thinks programming should feel like. Think Paul Graham essay, not product blog post. Serif. Wide margins. Dates at the bottom.

### 7. Install + docs + community

One block, compact. This is the *bottom* of the page, not the top. A reader who made it this far doesn't need convincing.

```
Install:    curl -sSL install.plang.is | sh
Docs:       plang.is/docs
Source:     github.com/PLangHQ/plang
Community:  discord.plang.is
```

Note: the install line is itself a line you'd run. Consistent with the theme.

### 8. Footer — honest and short

Cost transparency. Version number. Author credit. License. No cookie banner theatrics. No "Trusted by" logos (don't get tempted later).

---

## Visual language

### Typography is the hero

PLang *is* text. The website should be, too. Recommended:

- **Body prose:** a humanist serif with personality. Not Georgia. Try *Söhne Breit*, *Tiempos Text*, *Untitled Serif*, or *PP Editorial New*. Something that feels like a printed essay.
- **Code:** a real monospace with character. *Berkeley Mono*, *JetBrains Mono*, *iA Writer Mono*, *IBM Plex Mono*. Not the default `Menlo`.
- **No sans-serif at all.** This is a bold choice and it's the right one. Sans-serif is the font of every developer homepage. Absence is the signal.

Size: code lines in the parade should be *large*. 32–48px. The reader should feel like the code is physically important on the page.

### Color

Three palettes to pick from. Designer picks one and commits:

1. **Ink on paper.** Cream (#F5F1E8) background, near-black serif (#1A1A1A), deep red (#A13A2C) for code highlights. Feels like a published book. Confident.
2. **Terminal inversion.** Near-black (#0E0E0E) background, off-white type, a single signature color (phosphor green, amber, or a clean cyan) for the PLang `%variables%`. Feels like the tool is running right there on the page.
3. **Quiet white.** Pure white, one dark gray for text, one accent (muted blue or warm orange) used *only* on `%variable%` highlights within code. Swiss. Spare. Feels expensive.

Do not use gradient buttons. Do not use drop shadows. Do not use glassmorphism. The visual vocabulary is: type, space, rule lines, one accent.

### Whitespace is the message

Current site is dense. The new one should be *almost embarrassing* in its sparseness — vast margins, breathing between sections, single lines sitting alone in a viewport. The page is saying: we removed everything. That's the product.

---

## Motion and interaction

Default: **nothing moves**. The absence of motion is a choice.

Approved motions:

- Code lines typing themselves out *once* when scrolled into view. Never loop.
- A cursor blink at the end of one hero code line. Subtle. Almost missable.
- The REPL result appearing, no animation, just present.

Forbidden motions:

- Carousels.
- Parallax anything.
- Text that slides in from the side.
- Cards that tilt on hover.
- Marquee logo strips.
- Number counters (`10,000+ developers`).

If a motion would look at home on a 2022 Webflow template, it does not belong here.

---

## Wild ideas (pick at least two; they are the difference between a good site and a memorable one)

1. **The page source is .goal files.** View-source on plang.is returns PLang code, not HTML. The website is written in the language it is selling. This is plausible because PLang can serve web pages.
2. **Every section heading is itself a line of PLang.** `- show hero` `- show parade` `- show philosophy`. The page structure is legible as code.
3. **The install link is a line of PLang.** Clicking it copies `- install plang` to the clipboard. Running it in any PLang environment installs the language. Meta-consistent.
4. **The pricing section is a live PLang program.** It compiles one line of PLang while you watch and shows the actual cost. "$0.019 — you just saw the compiler run."
5. **A polyglot demonstration.** A single code block morphs from English → Icelandic → Japanese → Portuguese. Same program, different surface. Cycles once per page load, then stops. Proves that "natural language" means *any* natural language, not just English.
6. **The 404 page.** A line of PLang: `- if page not found, suggest something`. It then suggests something.
7. **Scroll-locked compiler.** As the reader scrolls through the parade, a small persistent indicator in the corner counts up the cost of compiling everything they've seen so far. `$0.143 compiled so far.` Ends at the cost of building a small app. Makes the pricing concrete without a pricing section.

---

## Reference aesthetics (mood board directions, not templates)

- **Stripe's clarity, not its density.** Stripe's homepage is beautiful but still has the shape of a product page. Keep the clarity, break the shape.
- **Readymag editorial sites.** The long-scroll essay feel where typography carries narrative.
- **Linear's manifesto pages.** Confidence without bragging.
- **Japanese poster design (Tanaka, Yokoo, Nagai).** Radical whitespace, heavy type, single accent. No decoration that doesn't earn its place.
- **Early Craigslist / early Hacker News.** Not visually, but philosophically — the refusal to perform professionalism *is* the professionalism.
- **Bret Victor's personal site.** Prose-first, demo-second, navigation last. The reader is trusted to scroll.

Avoid: anything resembling Vercel, Supabase, Linear's landing page (the manifestos are great — the landing isn't), generic developer-tool homepages with gradient hero + three-column feature grid.

---

## Anti-patterns (absolute no-fly list)

- No "Trusted by [logos]" strip. Ever.
- No testimonials carousel.
- No modal asking for email before showing the product.
- No cookie banner more prominent than the content.
- No "Book a demo" anything.
- No chatbot icon in the corner.
- No feature comparison table.
- No "Built with PLang" badge in the footer. (Except — if the site *is* built with PLang, then yes, one small note at the very bottom. That's earned.)
- No "hero image" — PLang has no hero image. The code is the hero.
- No sticky nav bar. The reader scrolls because the content pulls them, not because a bar reminds them they can.

---

## Tone guide

Write like:

- An artist's statement, not a product page.
- A short story, not a sales funnel.
- Someone who built something because they had to, not because there was a market.
- A language designer talking to other language designers — even though the audience is broader. Punch up. Assume intelligence.

Avoid:

- "Supercharge your workflow"
- "Unlock the power of"
- "The future of programming is here"
- "10x faster"
- Any sentence that begins with "Imagine"
- Any sentence that contains "seamless"

If you would be embarrassed to read it aloud, cut it.

---

## What success looks like

A developer opens plang.is, scrolls for 30 seconds, reads maybe seven lines of code total, and closes the tab feeling vaguely unsettled — like they just saw a magic trick and can't figure out how it works. They come back the next day and try the REPL. By the end of the week they've written their first `.goal` file.

No pitch was made. No feature was sold. They were *shown* the language and they understood.

That is the site.

---

## Final note to the designer

Everything above is direction, not specification. You are invited to disagree and propose better — but if you propose better, the replacement must be *more* radical, not less. The failure mode of this project is designing a beautiful version of the current site. The success mode is designing something that doesn't look like a language homepage at all.

Go make it strange.
