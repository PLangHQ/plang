# v2 plan: independent OBP re-check of the door reshape (decision 184)

Scope: `git diff 6ff57ceb4^ 12fa4b1bd -- PLang PlangConsole PLang.Generators` (172 files). Read only.
Method: the new rule "Jump to where the code lives" (if/switch on what a thing is, null check on a reference, loop telling elements their place, lookup beside the natural call), then the named smells (fork, stray helper, opened box, late stamp, creative name, verb+noun, stored twice, flat copy).
Focus: app/View.cs, item Write virtuals, type.Face/Identity, formal writer record door, size/duration subclasses, item's base walk over Host.
Claims 167-183 are checked against code, not trusted. The auditor's report is not read.
Output: v2/report.md (verdict per finding: holds / fix at the root; why-chain with file:line; list of new names), verdict.json, report.json session, commit .bot/item-doors/codeanalyzer/ only.
