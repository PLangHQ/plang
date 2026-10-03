# v4 — short lessons: the curriculum

Ingi (2026-10-01): "start to do more simple lessons: how to read file, how to filter list, what is condition,
how does foreach work, http in plang, db in plang, etc. Write up a plan, then read it over and categorise and
structure them for good teaching."

The plan itself (two passes: the raw list, then the reading-over and the structured curriculum):
`/shared/educator/course/CURRICULUM.md`.

In short: 26 one-idea lessons in five tracks (holding data, deciding and repeating, files, the outside world,
making it solid), one running example (the shopping list), one lesson shape (question, the step, build and
run, what plang wrote, try it, where to look). Database is blocked (no db module on app-systems); text
templates wait for the load-vars fix.

Next: Ingi reads the plan; then Track A, starting with A4 "filter a list" once A1-A3 exist.

## Progress (2026-10-01)
- Track A: A1-A4 built, voiced, checked, exported, on the course page. Shared short-lesson scenes in the engine (film/short: question, wrote, tryit); film.example gets a wide-output layout.
- A5 held (builder issue 18: plain sort compiled as descending; list.query replacing where/sort/group, decision 419). A6 held (Ingi: number is a double; the output formatter will print the culture's decimals).
- Also: docs/Modules.goal fixed (v0.2 loops), landed on app-systems aaa127a11; generated file.md = golden; publish waits for read.guide.md (load-vars fix, Ingi ruled: user variables only).

## Progress (2026-10-02/03)
- A5 (bare sort writes back), A6, B1-B5, C1-C4 exported. Builder issues 21/25/28/32 found, traced and fixed by others; the studio was migrated to the new hash without re-voicing.
- docs/Modules.goal writes os/system/modules/<m>/start.md for file, condition and loop (app-systems 7460cdd94); the hand-written pages were removed (Ingi, e5073b0ef).

## Track D plan (Ingi OK'd the APIs, 2026-10-03)
- D1 talk to the user: `ask`, `write out`. No network. Ask is interactive, so check how a lesson run answers it (piped input).
- D2 read from the web: `get https://api.frankfurter.app/latest?from=EUR&to=USD`, use a field (`%rates.rates.USD%`). The rate changes daily, so the lesson says so and output.txt is that day's.
- D3 send to the web: `post` the shopping list as JSON to `https://httpbin.org/post`, show what came back (`json`). Never a key or header with a secret (echo service).
- D4 download: a CSV from a stable URL, then C3's read. D5 render: an HTML receipt. D6 llm.query: needs a key at run time, so check how a learner's key is set before writing it.
- Each: build fresh 2-5x, run, check the .pr, write lesson.md, then the studio lesson, voice, check, export, hub. Findings go to the architect; friction to `.bot/app-systems/educator/friction.md`.
