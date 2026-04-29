# Security audit — runtime2-builder-bootstrap

## v1 (2026-04-29) — [details](v1/summary.md)

PASS. First security pass on the builder-bootstrap branch. 2 medium info-
disclosure findings (ParamSnapshot bypasses `[Sensitive]`, standing
`Variables.Snapshot/GetAll` finding confirmed) and 4 low tripwires (JSON-clone
silent fallback, FluidProvider recursion, traceId path component, intentional
BuildingGuard removal). No critical/high open. Auditor recommended next.
