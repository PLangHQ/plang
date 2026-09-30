# handoff — app-systems coder

375 (2) landed (decisions 389, 392): a `!x` root the memory doesn't bind is the app's member, else the module by
that name; a setting is always under `.setting` — `%!llm.setting.cache%`, `%!llm.query.setting.cache%`,
`%!build.setting.cache%`. A setting class's path is its read path (`llm.setting`); the module and the action answer
`.setting` themselves (item's `Setting` is virtual). Rows saved as `user!llm` are no longer read (no migration).

A module whose name is an app member (goal, module, test, variable, cache, code) is shadowed in the short form;
its file path `%!module.variable.set.setting.x%` is 375 (3)'s to make work. When 380 (4) lands, the module-by-name
step goes and short names become dynamic variables set in /system/on/create.goal.

## Next

375 (3) the current-node rule (+ `%!module.x…%`); (4) the event node (shape first); (5) the registrations sweep.
380 (4) `system.on.create` (shape first); 380 (5) read-as-text verbatim.

Waiting on Ingi: the http `response` reshape (387) — http/ belongs to the fix branch `app-systems-http` until it
comes back. 