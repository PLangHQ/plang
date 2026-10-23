# v4 plan: re-check of v3 section A (architect triage, decision 279)

Scope: coder commits 46f7965b5..HEAD (24 coder commits, 223 production files). Per A item (A1-A4, A6-A15; A5 moved to B): fixed at the root / partly / not, file:line, plus NEW smells those commits introduced (v3 checklist incl. names). Not re-raised: B, C (declined, with decision), D.
Method: read each commit's diff and the touched files whole; scan the added lines of 46f7965b5..HEAD for the v3 greps (is/as/typeof/ReferenceEquals, sentinels, throwaway Data, role words, verb+noun). Read only; commit only the report.
Output: v4/report.md, verdict.json, summary.md, report.json.
