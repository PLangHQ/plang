# educator: summary

**Version:** v4 (2026-10-01 → 2026-10-03), the short-lesson curriculum. Plan: `v4/plan.md`; curriculum:
`/shared/educator/course/CURRICULUM.md`.

## What this is

Teaching material for developers new to plang: short, one-idea lessons, each written as text plus code that
was built and run for real, then animated with narration and exported to mp4.
- **course** (`/shared/educator/course/<track>/`): `lesson.md` plus `code/<lesson>/` (Start.goal, data, `output.txt`, `.build`).
- **studio** (`/shared/educator/studio`): a plang app that voices a lesson (Gemini TTS via OpenRouter) and writes its
  page, plus the JS animation engine. Node + Playwright check and export.
- **course page**: `/shared/educator/index.html` (Windows `C:\Dev\claude\shared\educator\index.html`), written by `studio/Hub.goal`.

## State

| Track | Lessons on the course page | Held |
|---|---|---|
| intro | 1 what is plang, 2 first program, 3 OBP, 4 OBP smells | — |
| A holding data | A1–A6 | — |
| B deciding and repeating | B1–B5 | B6 sleep (builder issue 19) |
| C files | C1–C4 | — |
| D the outside world | D1–D5 | D6 llm at run time (coder: Conversation misread, template in the message, ask-once key flow, decision 598); D7 database (no db module) |
| E making it solid | — | not started |

Also:
- **Module pages:** `docs/Modules.goal` writes `os/system/modules/{file,condition,loop}/start.md`. It saves through
  `/system/…` (decision 522) and is on app-systems as 7460cdd94. The hand-written `docs/modules/{file,condition,loop}.md`
  were removed (Ingi, e5073b0ef). The loop page's regeneration for foreach's `Parallel` is held: both new parallel
  examples fail (run-time `%item%` error; the build crashes on `wait for %task%`). It's with plang-75.
- **Friction review** (Ingi's ask: a user's review of the builder): `.bot/app-systems/educator/friction.md`, on this branch.

## Key decisions

- Every lesson claim is built in fresh folders (2–6×) and run. The `.pr` is checked, not just the output. Probes
  look like the lesson code, with no explaining comments (a comment steers the builder).
- When a step builds wrong, trace what the model was given before reporting:
  `--debug={"llm":{"system":true,"user":true,"response":true},"goal":"NoSuchGoalX","length":{"max":50000}}`.
- Track D uses api.frankfurter.app and httpbin.org (Ingi OK'd). Never a secret to httpbin. D6 teaches nothing about
  keys (plang asks once, decision 598).
- Pushing to app-systems needs Ingi's yes each time (fast-forward, no force).

## Code example

The shape every short lesson's code takes (D2):
```plang
Start
- get https://api.frankfurter.app/latest?from=EUR&to=USD, write to %rates%
- write out "1 euro is %rates.rates.USD% dollars, on %rates.date%"
```
and its "what plang wrote", read from `.build/start.pr`:
```
http.request    Url: https://api.frankfurter.app/latest?from=EUR&to=USD
variable.set    Name: %rates%   Value: %!data%
```

## Next

- D6 when the coder's three changes land. Then the loop page (re-pin its golden) when the parallel examples run.
- Track E. A6's optional `0.1 + 0.2` line and C1's one-step exists at their next re-export.
