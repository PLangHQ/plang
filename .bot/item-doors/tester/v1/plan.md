# tester v1 — test speed (brief: .bot/item-doors/architect/briefs/test-speed.md)

1. How TUnit runs the suites: parallel or not, workers, wall vs CPU (Types, timed at default/serial/8, load recorded).
2. What tests change on an app, per kind (grep, pattern recorded).
3. After kinds + fix B: profile 100 warm births with temporary stopwatch marks (reverted, never committed), split Registry.Load/Enlist, module, and the rest of the app.
4. Weigh designs with Ingi (pool vs shared facts vs made on first ask). No production change before his yes.

Output: .bot/item-doors/tester/test-speed.md.
