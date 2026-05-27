# Semgrep Rules — PLang Security Invariants

Static rules that enforce architectural security invariants documented in
`Documentation/v0.2/good_to_know.md`, `CLAUDE.md`, and the security bot's
memory. These run in seconds and catch silent violations during refactors.

## Run

```bash
# Install once
pip install semgrep

# Run all rules against the PLang library
./scripts/semgrep-scan.sh

# Or directly
semgrep --config .semgrep/ PLang/
```

## Rules

| File | Severity | What it catches |
| --- | --- | --- |
| `console-write-ban.yml` | WARNING | `Console.WriteLine`/`Console.Error.Write` outside the permitted exception (PlangConsole/Program.cs). Documented ban: `good_to_know.md` "Console.* Is Banned in Production C#". |
| `assembly-loadfrom-whitelist.yml` | WARNING | `Assembly.LoadFrom` / `LoadFromAssemblyPath` outside `FilePath.LoadAssemblyAsync` (the gated canonical site). Catches new code-execution sinks. |
| `serializer-hygiene.yml` | INFO | Bare `JsonSerializer.Serialize`/`Deserialize` (no options object). Used as an audit checklist — each hit needs eyeballing for `[Sensitive]` / `path.@this` / cycle reachability. |
| `lock-public-collection.yml` | WARNING | `lock (other.X)` cross-file lock target — OBP Shape Smell #2. |
| `verified-setter.yml` | ERROR | `public Verified` or `Signature` property with public setter. Critical: a settable trust marker is an RCE primitive. |

## Severity contract

- **ERROR** — must not land. CI gate candidate.
- **WARNING** — review at PR time; tune the exclude list if a new legitimate site lands.
- **INFO** — audit checklist; not blocking. Walk through during security reviews.

## When a rule fires legitimately

- A new exempt site (e.g. another canonical home for `Assembly.LoadFrom`):
  add a `paths.exclude:` entry with a comment explaining the carve-out.
- A new banned-pattern variant: extend `pattern-either:` in the relevant
  rule file.
- A new architectural invariant emerged from a real incident: add a new
  rule file. Cite the incident in the message.

## Limitations

- Free Semgrep is pattern-matching, not taint analysis. Reachability ("a
  `[Sensitive]` value flows into this serializer") needs Pro.
- C# parser at ~99.9% coverage on this codebase. Files using exotic syntax
  may partial-parse — verbose mode (`--verbose`) shows skipped ranges.
- Rules drift with the codebase. Treat them like tests — when an
  architectural rename happens, audit the exclude lists.

## CI integration

Not wired today. When desired, drop a GitHub Action that runs
`semgrep --config .semgrep/ --error PLang/` on PR. The `--error` flag
makes WARNING+ block.
