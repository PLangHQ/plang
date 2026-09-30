# v2 plan: lesson 2, "your first plang program" (hands-on)

## Why
Ingi, 2026-09-30:
> "More of a teaching environment … do it like you are teaching somebody. First download from plang.is/start.ps1, install, open code editor, start writing code, build, run, and then do more complex goals."

Lesson 1 ("What is plang") stays as the concept intro.

## Decisions (Ingi)
- **Install:** teach `irm https://plang.is/start.ps1 | iex` as the way it will work. It isn't live yet: the URL serves the homepage, and the version and sha256 are placeholders.
- **Editor:** PlangOS's own editor.
- **Screens:** from the os bot (session `os`), as stills and videos, in `/shared/educator/os/first-program/`, each with a `.md` note. Requested 2026-09-30 (shots 01-08 plus the facts about how to build and run in PlangOS).

## The learner's path (draft; each claim traced before it's used)
1. **Install.** PowerShell, then `irm https://plang.is/start.ps1 | iex`. What it does, from `/shared/plangos/start.ps1`:
   - installs to `%LOCALAPPDATA%\PlangOS` and checks the download's sha256;
   - makes `.goal` files open with plang (double-click runs, Edit opens Notepad), per user, no admin;
   - runs `Start.goal`, which installs WSL, imports the PlangOS image and opens the PlangOS window.
2. **PlangOS opens.** Screen 03.
3. **Open the editor, make Start.goal.** Screens 04-05. Waiting on the os bot's answer.
4. **Write the first goal:** hello, with a variable (verified: `work/hello`).
5. **Build, then run.** Screens 07-08. Show the builder's real output as text:
   ```
   Building goal: Start
     Saved Start (…)
   ```
6. **More complex goals, one idea each, each verified on the branch** (`work/first-program`):
   - ask the name, then greet: **blocked** (FINDINGS 25, 26);
   - a condition: **blocked** (FINDINGS 23, 25);
   - a list with `foreach` calling a second goal: verified;
   - save to a file, read it back: verified;
   - (maybe) one that calls another goal with a parameter.

## Library work this needs
- **`film.video`:** screen recordings drawn on the canvas, locked to film time. Seek, scrub and stills must work, so the video follows `frame.t`, not its own clock. Videos are inlined like images (checked when the first video arrives; the `.png` render bug may also apply to `.mp4`, FINDINGS 21).
- Screenshots are drawn as "photos taped on the page" (paper piece plus tape), with a highlight on the part the voice is talking about.

## Blocked
- The os bot's screens and facts (steps 2, 3, 5).
- Beginner-step bugs, reported to the architect in `/shared/architect/educator-beginner-steps.md` (step 6).
