# Security Audit Plan — runtime2-setup-goal v1

## Scope

This branch adds the Setup.goal run-once execution system on top of the DataSource + Settings bridge (from runtime2-system-datasource). New/changed code:

1. **Setup/this.cs** — Run-once execution tracker using engine.System.DataSource, keyed by step.Hash
2. **Steps/this.cs** — RunAsync with setup run-once semantics (skip executed, record on success/tolerated, abort on record failure)
3. **PLangContext.Setup** — New property propagated through context, copied in Clone()
4. **EngineGoals** — Setup property, AllIncludingSetup internal, Get() excludes setup goals
5. **Executor.Run2** — Wires Setup.RunAsync before main goal
6. **Goal.Methods.cs** — Minor refactoring
7. **IsTolerableError** — String-matching error messages ("already exists", "duplicate column name")
8. **DataSource/Settings files** — Carried from parent branch (already audited on runtime2-system-datasource)

## Phase 1: Blue Team (Attack Surface Mapping)

Map trust boundaries specific to the Setup system:

1. **Hash-based step tracking** — step.Hash as execution key. Who controls hashes? Can they be pre-seeded or deleted?
2. **IsTolerableError string matching** — Attacker-controlled error messages matching "already exists" → error suppression
3. **Setup context propagation** — context.Setup propagates through goal.call, giving called goals run-once semantics
4. **Record metadata** — Writes goal path, step index, step text, timestamp to DataSource. Information stored.
5. **Setup goal ordering** — "Setup" first, then alphabetical. Race conditions in ordering?
6. **Setup goal exclusion from lookup** — Can a non-setup goal be marked IsSetup to hide from Get()?
7. **Executor.Run2 integration** — Setup runs before main goal. What if setup hangs or exhausts resources?
8. **Cross-feature: Setup + DataSource** — DeserializeValue still has unfixed InvalidOperationException gap

## Phase 2: Red Team (Attack Vectors)

For each surface, construct attack scenarios:

1. Hash collision → step skip (feasibility assessment)
2. Controlled error message → setup-tolerable bypass
3. Setup context leak → non-setup goal gets run-once semantics unexpectedly
4. Record metadata → information disclosure via DataSource dump
5. Infinite setup loop → goal.call during setup calls itself
6. Setup goal "hiding" → marking goals as setup to exclude from API
7. Resource exhaustion via setup steps → unbounded table creation
8. Carry-forward: DeserializeValue InvalidOperationException

## Phase 3: Report + Learnings

Write security-report.json, verdict.json, summary.md, learnings.
