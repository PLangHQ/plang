# plang.is — Design Brief (v2, Monastery)

**Audience:** you, the designer.
**Author:** coder (relaying Ingi's design direction).
**Theme:** Tibetan monastery. High altitude. Stone, snow, silk, ink, silence.
**Relationship to v1:** Same language (PLang), same thesis (radical simplicity), different vessel. v1 was a parade. v2 is a pilgrimage.

---

## Begin here

In a monastery, a monk points at a bell and says:

```
set %!app.debug.enable% = true
```

He does not strike it. He waits.

You understand, or you do not.

---

## The one idea (same as v1, differently held)

One line of PLang does what fifty lines of another language do. That is not a feature — it is a property of the language, the way light is a property of the sun. The website's job is not to *explain* this. The website's job is to build a quiet room in which the reader can notice it.

v1 (the parade) tries to surprise the reader.
v2 (the monastery) tries to still them.

Both aim for the same realization. One shouts; the other whispers. Pick the one that fits the moment.

---

## What the current site misses (same diagnosis, softer hand)

The current plang.is is not wrong — it is dressed for a different occasion. It wears the clothes of a language launch: navigation, claim, grid, pricing, community. Those clothes fit many things, but not this language. PLang does not need to be sold. It needs to be *shown*, and then sat with.

Everything we took apart in v1 applies here. The only addition: the current site is *loud* for a language whose power is *quiet*.

---

## The new direction, in one breath

> The website is a pilgrimage. The reader arrives at a base camp. They walk through teachings — each a single line of code, each with space to breathe. They reach a monastery, read a short sutra, and leave changed. There is no pitch, no button press, no urgency. The silence does the work.

---

## Structure as pilgrimage

### 1. The threshold (hero)

A long pause before any content appears. Not a loading spinner — a deliberate empty screen for two to three seconds. Then, slowly, one line resolves in the center:

```
set %!app.debug.enable% = true
```

Below, in small, almost apologetic type:

> a single line lights the whole house.

Nothing else. No nav bar. No logo wall. No scroll hint. The reader finds the path by looking for it.

### 2. The teachings (the parade, re-tempered)

Same eight to twelve one-liners as v1, but the pacing is changed. Between each teaching is a **breath** — an empty viewport of stone-cream, sometimes containing a single rule line, sometimes containing nothing at all. The reader scrolls past emptiness, arrives at the next line, and the line is there alone.

Half the teachings have captions. Half do not. The absence is deliberate — the reader must sit with the code itself.

Example cadence:

```
- every morning at 7am, send weather to %me.phone%
```

*(scroll — empty viewport — scroll)*

```
- on error retry 3 times, call ChargeCard
  the path is walked three times before it is abandoned.
```

*(scroll — empty viewport — scroll)*

```
- foreach %products%, call PriceIt item=%product%
```

*(scroll — long empty stretch — scroll)*

```
- call airline.Book, on rollback call airline.Cancel
  what is done can be undone. the language remembers how.
```

The captions, when they exist, read like koans — not marketing. They do not explain what the code does (the code already does that). They point at what the code *means*.

### 3. The sutra (the thesis, as scripture)

After the teachings, the reader arrives at a single long passage. Set in wide-margined serif, two to three short paragraphs. Plain voice. This is where the explanation finally happens:

> PLang is written in natural language. A large language model compiles your intent into executable steps. The language is not English with code sprinkled in — it is a formal language whose surface happens to read like English, or Icelandic, or Tibetan. The runtime knows the difference between the surface and the program. The surface may change. The program is what runs.
>
> Version 0.1. Each line of code costs between $0.005 and $0.035 to compile, one time. Once compiled, it runs forever. You are paying the LLM to translate your intent into a form the machine can remember.
>
> Everything else about the language follows from this.

That last sentence is the moment. Leave it alone on its line.

### 4. The bell (the comparison)

One comparison. Not ten. A single line of PLang, still and alone on the page. Beneath it, collapsed, a note: *the same program, in Python — forty-seven lines.* The reader can expand it, or not. Most will not. The collapsed count is the teaching.

### 5. The practice (REPL)

A plain input. No border, no shadow, no call-to-action label. Just a prompt:

```
-
```

The reader types. The program runs. The result appears below, unhurried. No confetti. No "you've done it!" No analytics event.

If a live REPL is too much for v1 of the site, a scripted one — a single pre-filled example that runs step by step on click — is the minimum. A screenshot is not.

### 6. The reading room (long-form)

A link, not a section. One line: *further reading — a letter from the author*. Clicking it opens a long essay by Ingi: why he built the language, what he gave up, what he believes programming should feel like. Wide margins. Drop cap. Serif that feels printed. Date at the bottom, signed.

This is not the homepage. It is the alcove off the main hall, for those who want to sit longer.

### 7. The gate (install, docs, source)

Compact. Bottom of the page. Four lines:

```
install    curl -sSL install.plang.is | sh
docs       plang.is/docs
source     github.com/PLangHQ/plang
gather     discord.plang.is
```

The install line is a command, not a button. The reader copies it because they have decided to, not because a CTA told them to.

### 8. The colophon (footer)

Short. Honest. In the style of a sutra's closing dedication:

> plang is made in iceland, in the cold months, by a small number of hands. the language costs what the compiler costs. the source is open. version 0.1. if you build something with it, write to plang@plang.is.

No cookie banner more prominent than this. No social icons cluster. No "© 2026 PLang, Inc." — the word *made* is warmer and truer than *copyright*.

---

## Visual language

### The palette (one, not three — this is a monastery, not a showroom)

- **Ground:** rice-paper cream (#F2ECDF). Warm, aged, handled. Never pure white.
- **Ink:** deep charcoal, almost black (#1C1A17). All body and code.
- **Robe:** burgundy red (#8B2A1F). Used only on `%variables%` inside code, and on a single horizontal rule that appears once per section break. Holy — do not spend it on buttons.
- **Sky:** high-altitude blue (#3A5A78). Used almost nowhere. Perhaps for the external-link underline, and nothing else.
- **Snow:** pure white (#FFFFFF). Reserved for the breath-viewports between teachings. The absence color.

No gradients. No shadows. No glass. Rule lines are 1px, ink, hair-thin. Borders, where they exist, are implied by space, not drawn.

### Typography (one body, one code, and that is all)

- **Body:** a serif with presence and warmth. *Tiempos Text*, *PP Editorial New*, *Plantin MT*, or *Garamond Premier Pro*. Something that reads like a printed book. Italics are permitted and encouraged.
- **Code:** a monospace that feels inked. *Berkeley Mono* or *iA Writer Mono* are ideal. Avoid anything that feels like terminal glow — PLang is not code-on-a-screen, it is writing.
- **Headings:** the same serif, lowercased, italicized if possible. Never bold. Never all-caps. Never sans-serif.

No sans-serif anywhere on the page. Absence is the signal.

### Whitespace as ground truth

The ratio of empty space to content should embarrass the designer slightly. If it feels sparse, it is not sparse enough. The monastery is vast; the monk has few possessions. The site is vast; each element is singular.

Section breaks should be *full viewport heights* of empty cream. The reader scrolls through nothing to arrive at the next teaching. That nothing is the teaching too.

### The prayer flag (one recurring motif)

A horizontal line of code, running the full width of the content area, in five colors — each a different natural language that PLang compiles from. English, Icelandic, Japanese, Tibetan, Portuguese. The same program, each line a different surface. Appears once on the page, near the sutra. Static. Not animated. The reader sees all five at once and understands: *the surface does not matter.*

---

## Motion and sound

### Motion

**Default: nothing moves.** Stillness is the aesthetic and the argument.

Exceptions, each used at most once per session:

- The hero's opening line *fades in* over two seconds after the initial pause. Never again.
- A single hair-thin rule drifts across the screen, left to right, at the transition between "teachings" and "sutra." Like a prayer flag string swinging once in wind. Four seconds. Then nothing, forever.

No hovers that animate. No scroll-triggered text slides. No parallax. If a motion would appear in a product launch video, delete it.

### Sound

**Forbidden by default.** No autoplay. No background ambient track. No scroll-triggered audio.

**One permitted secret:** a small glyph in a corner — perhaps a single bell shape rendered in ink. Clicking it plays a singing-bowl tone, exactly once. The tone lasts eight seconds. It plays again only if the reader refreshes. This is not advertised anywhere on the site. It is discovered by those who look.

---

## Wild ideas (pick at least two; these are the difference between devotion and decoration)

1. **The pause.** The first three seconds of every visit are empty. Pure cream ground, no content. The reader learns patience before they learn the language. (The designer should fight for this even when the product manager says "bounce rate.")
2. **The bell, as above.** A hidden click that plays a singing bowl. Known only to those who stay long enough to find it.
3. **The Tibetan teaching.** One of the parade viewports shows a line of PLang written in Tibetan script — no transliteration, no caption. Beneath it, far below on the page, the same program appears in English. The reader understands without being told: the language is the program, not the surface.
4. **The step counter.** A tiny glyph in the corner records how far the reader has walked. "42 steps." The pricing section is simply: *one step of PLang costs between $0.005 and $0.035. You have walked 42.*
5. **The mandala.** A diagram, hand-drawn-feeling, where the center is a single PLang step and the outer rings are all the capabilities it can invoke — DB, HTTP, LLM, filesystem, scheduler, auth, cache. Static. Black ink on cream. Labeled in small italic serif. The reader sits with it the way one sits with a painting.
6. **The dedication.** Somewhere in the footer, dated and signed, a short passage: *this language was carved during the winters of 2023 through 2026, by ingi and a small sangha of contributors, in iceland.* The word *sangha* does real work here.
7. **The 404 page.** "You have strayed from the path." Below, a small note: *perhaps you meant* → (a suggestion). The serif is the same. The ink is the same. The silence is the same. Even the error is the monastery.
8. **The scroll as breath.** Long-content pages have a vertical rhythm tuned to natural reading breath — text, pause, text, pause, text. The page is paced like a chant, not an infinite feed.
9. **The colophon line.** At the very bottom of the site, a single Tibetan character: 空 (*emptiness*). Unexplained. Some will recognize it. Some will not. Both are correct.

---

## Reference aesthetics

- **Shunryū Suzuki's *Zen Mind, Beginner's Mind* (the book object, not the text).** Plain cover, heavy paper, generous margins. The object itself is the teaching.
- **Muji catalogues.** Disciplined emptiness. Everything labeled, nothing sold.
- **Hiroshi Sugimoto's photographs.** Long exposures. A horizon line. Nothing happening, and yet everything.
- **Agnes Martin's grids.** Soft color on cream, no center, no star. The whole is the point.
- **Bret Victor's website.** Prose-first, demo-second, the reader trusted to stay.
- **Old Tibetan sutras.** Horizontal manuscript pages, handset type, wide margins, red for the opening syllable of each prayer.
- **Kyoto temple websites (the good ones — not the tourist ones).** The gardens speak; the interface apologizes for being there.

Avoid: Vercel, Linear (their landing, not their manifestos), Supabase, any developer-tool homepage with a gradient hero and a CTA cluster. If the site looks like it belongs in a VC portfolio, it has failed.

---

## Anti-patterns (non-negotiable)

- No "Trusted by [logos]" strip.
- No testimonials.
- No email capture modal. Ever.
- No cookie banner more prominent than the content itself.
- No "Book a demo." No "Contact sales."
- No chatbot icon. Especially no AI chatbot icon.
- No feature comparison table.
- No sticky nav bar. The reader's spine does the navigation.
- No "hero image." PLang has no hero image. Code is the image.
- No dark mode toggle. Pick one palette and inhabit it. The monastery has one light.
- No sans-serif fonts.
- No motion that would appear in a product launch video.

---

## Tone guide

Write like:

- A sutra, not a sales page.
- A letter from a friend who lives far away and writes once a year.
- A craftsman describing a tool, not a founder describing a company.
- Someone who is not in a hurry and does not need you to be either.

Avoid:

- "Supercharge your workflow"
- "Unlock the power of"
- "The future of programming"
- "10x faster"
- "Imagine if..."
- "Seamlessly"
- Any exclamation mark anywhere on the page.

If a sentence would sound correct coming from a SaaS CEO at a product launch, rewrite it or cut it.

---

## What success looks like

A developer opens plang.is, and for three seconds, sees nothing. They almost close the tab. Then one line of code appears, and they stop.

They scroll. They read seven lines of code total, and three short pieces of prose. They spend more time on the empty stretches than on the content. They do not press any buttons. They do not submit any forms.

They close the tab and go to sleep.

In the morning, they cannot explain why, but they install PLang. Later in the week they write their first `.goal` file. Six months later they tell someone else about it, and they point at the site, and say, *just go there. just scroll.*

That is the site.

---

## Final note to the designer

Everything here is in service of one discipline: **subtraction**. Every time you are tempted to add, subtract instead. Every time you are tempted to explain, remove the explanation. Every time you are tempted to decorate, let the space stand.

If the page feels almost empty — keep going. Make it emptier. The monastery is not built by what is put there; it is built by what is kept out.

The test: sit with the design for five minutes in silence. If you feel anxious, it is still too loud. If you feel still, it is done.

Go make it quiet.

---

*v1 (the parade) and v2 (the monastery) are both valid. v1 is louder, more confident, more American. v2 is quieter, more anchored, more Tibetan. The same language is underneath. Choose the vessel that matches the moment — or mix them, if you find a way.*
