# v7 plan: the two files changed after review6 (rule 66, N12) and the full bench row (N15)

Tip 2401279ed. Read module/identity/code/Default.cs and type/item/image/this.cs whole (diff since review6: 23 lines, nothing else changed in PLang); report findings, blocking or not, older smells marked. Run Tools/Bench/run.sh on the tip (full), append the row to Tools/Bench/record.md, compare with stage5 and review4's row; a number outside its band is blocking unless a decision explains it; held memory growing with requests always blocking. Read only; commit .bot/fast-apps/codeanalyzer, the session log and the bench rows.
Output: .bot/fast-apps/codeanalyzer/review7.md, v7/verdict.json, summary.md, report.json.
