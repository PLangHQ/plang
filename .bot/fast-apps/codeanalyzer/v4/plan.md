# v4 plan: fourth review (decisions 81-85, tip 02aff152e) and the bench row

Scope: the 24 production files changed since review3 (f4f754455..02aff152e), each read whole, checked against review3's findings (blocking 1 Shipped, blocking 2 overflow check; 3-13) and for new smells (names included); the other touched files were read whole in review3 and are carried unless a changed hunk touches them. Stage 6: nothing in source names a removed package's namespace (grep of using/qualified names over PLang, PlangConsole, PLang.Generators, PLang.Tests, Tools; the build in the bench confirms).
Bench: Tools/Bench/run.sh on the tip at a quiet load, row appended to Tools/Bench/record.md against stage5's; a number outside its band is blocking unless a branch decision explains it; held memory growing with requests always blocking; the clean build especially.
Output: .bot/fast-apps/codeanalyzer/review4.md, v4/verdict.json, summary.md, report.json.
