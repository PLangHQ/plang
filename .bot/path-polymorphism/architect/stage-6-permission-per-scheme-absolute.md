# Stage 6: Permission per-scheme `Absolute`

**Goal:** `Path.Absolute` becomes scheme-defined canonical form. FilePath unchanged (OS-normalized). HttpPath gets URL canonical-form. Permission code itself stays scheme-agnostic — it calls `path.Absolute` and matches the resulting string.

**Scope:** HttpPath's `Absolute` implementation, the canonical-form rules, Permission grant/match semantics for HttpPath URLs.

**Out of scope:**
- New Permission verb shapes (Verb stays unchanged).
- Glob/Regex matching on URL paths (Match enum semantics carry over unchanged — Exact match against canonical-form is the default; users still pick mode).
- Per-host or per-domain shorthand grants — that's a future ergonomics concern.

## Design

### Canonical-form contract

`Path.Absolute` is a string that uniquely identifies the resource within its scheme. Two `Path` instances pointing at the same logical resource produce the same `Absolute`. Permission grants and requests match on this string.

### FilePath

Unchanged from stage 2 / today: OS-normalized absolute path. `/home/x/../y/z.txt` → `/home/y/z.txt`. Existing tests stay green.

### HttpPath

Canonical-form rules — pin each:

1. **Scheme + host lowercased.** `HTTP://Example.COM/foo` → `http://example.com/foo`.
2. **Default port stripped.** `https://example.com:443/foo` → `https://example.com/foo`. `http://example.com:80/foo` → `http://example.com/foo`. Non-default ports kept: `https://example.com:8443/foo` → `https://example.com:8443/foo`.
3. **Path normalized.** `https://example.com/a/../b` → `https://example.com/b`. Use `Uri` class normalization.
4. **Trailing slash policy:** `https://example.com` and `https://example.com/` are the same canonical-form. Pick one (the form *with* trailing slash on the root) and commit.
5. **Query sorted.** `https://example.com/?b=2&a=1` → `https://example.com/?a=1&b=2`. Sort keys lexicographically; preserve duplicate keys in their original order *within* the same key.
6. **Fragment stripped.** `https://example.com/foo#bar` → `https://example.com/foo`. Fragments are client-side and don't address a different server resource.

Document the rules as a comment on `HttpPath.Absolute`. The contract tests in stage 7 enforce each rule.

### Permission Authorize prompt

Reads naturally for URLs because `Absolute` is the URL string:

```
Allow worker to read https://api.example.com/users.json? (y/n/a)
```

No new prompt code needed — the prompt template already uses `path.Absolute`. The only thing changing is what that string contains for non-file schemes.

### Match modes on URLs

`Match.Exact` against canonical-form is sufficient for most HTTP grants. `Match.Glob` and `Match.Regex` apply to the canonical-form string — already string-based logic, no scheme awareness needed.

A reasonable HttpPath grant pattern is "anything under `https://api.example.com/`" using Glob: `https://api.example.com/*`. This works because the canonical-form is a string the glob matcher walks. No special code.

## Deliverables

- `HttpPath.Absolute` property implementation per the rules above.
- Comment on the property listing the rules (so future readers don't have to chase tests).
- No change to Permission code itself — it already consumes `path.Absolute` as a string.

## Tests

See `plan-test-designer.md` Stage 6. Key surfaces:

- One unit test per canonical-form rule (six rules, six tests, or one parametrized test).
- FilePath grants don't match HttpPath requests (different scheme prefix in Absolute).
- HttpPath grant `https://api.com/*` (Glob) matches `https://api.com/users` request.

## Risk

Low. The canonical-form rules are well-trodden ground (URL normalization is a solved problem). Risk is forgetting one rule and shipping a permission bypass — the contract tests in stage 7 backstop this.

## Migration cost

Zero. FilePath grants in existing sqlite stores stay valid (Absolute formula unchanged). HttpPath has no prior grants.
