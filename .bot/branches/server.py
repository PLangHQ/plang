#!/usr/bin/env python3
"""
Branches overview server — see all .bot/<branch>/ dirs at a glance.

Run:   python3 .bot/branches/server.py
Open:  http://localhost:8083

Left nav: all .bot/<branch>/ dirs (newest activity first).
Right pane: stage timeline showing which roles have produced output,
when, and what files they wrote.
"""

import http.server
import json
import subprocess
import time
from pathlib import Path

PORT = 8083
REPO_ROOT = Path(__file__).resolve().parents[2]
BOT = REPO_ROOT / ".bot"

# Canonical pipeline order. Anything else gets bucketed as "other" at the end.
# Canonical pipeline — these are expected to run on every branch. Order
# matters: dots in nav and rows in the right pane follow this order.
PIPELINE = [
    "architect",
    "test-designer",
    "coder",
    "codeanalyzer",
    "tester",
    "security",
    "auditor",
]
# Optional roles — recognized but not always run. Rendered right-aligned
# in the nav and below a separator in the pane, and never counted as
# "missing" in the "Not yet run:" list.
OPTIONAL = ["builder", "docs"]
# Roles we still recognize (don't bucket into "Other subdirs") but that
# aren't part of either flow.
EXTRA_ROLES = {"handoff", "learnings", "scaffolder"}
KNOWN_ROLES = set(PIPELINE) | set(OPTIONAL) | EXTRA_ROLES

# Tool dirs in .bot/ that are NOT branches.
NON_BRANCH = {"architect", "branches"}


def current_branch() -> str:
    try:
        out = subprocess.check_output(
            ["git", "-C", str(REPO_ROOT), "branch", "--show-current"]
        )
        return out.decode().strip().replace("/", "-")
    except Exception:
        return ""


_CACHE: dict = {"at": 0.0, "active": set(), "times": {}, "refs": {}, "verdicts": {}}
_CACHE_TTL = 300  # 5 minutes


def _git(*args: str, timeout: int = 30) -> str:
    return subprocess.run(
        ["git", "-C", str(REPO_ROOT), *args],
        capture_output=True, text=True, check=False, timeout=timeout,
    ).stdout


def refresh_cache() -> None:
    """Fetch remote refs, rebuild active-branch set, rebuild commit-time dict.

    Single refresh path so the two views can never disagree. `git fetch`
    populates `refs/remotes/origin/*` for branches we never checked out
    locally — without this, `git log --all` can't see commits unique to
    those branches, and they'd appear empty in the overview."""
    try:
        _git("fetch", "--quiet", "origin", timeout=30)
    except Exception:
        pass  # offline is fine; we keep stale data

    # Active set + slug→ref map. The map is needed because file content is
    # fetched via `git show <ref>:<path>` — we need to know which ref backs
    # each `.bot/<slug>/` directory. Remote refs win over local since the
    # remote is authoritative (bots push there).
    active: set[str] = set()
    refs: dict[str, str] = {}
    for line in _git("for-each-ref", "--format=%(refname:short)",
                     "refs/heads", "refs/remotes/origin").splitlines():
        ref = line.strip()
        if not ref or ref == "origin/HEAD":
            continue
        if ref.startswith("origin/"):
            slug = ref[len("origin/"):].replace("/", "-")
            refs[slug] = ref  # remote overrides local
        else:
            slug = ref.replace("/", "-")
            refs.setdefault(slug, ref)
        active.add(slug)

    # Commit-time dict. `--diff-merges=first-parent` is critical: without it
    # merge commits that *introduce* files (e.g. "merge origin/runtime2 into
    # branchX") are silent under `--name-only`, so merge-introduced files
    # would be invisible to the scan.
    out = _git("log", "--all", "--diff-merges=first-parent",
               "--format=|%ct", "--name-only", "--", ".bot/")
    times: dict[str, float] = {}
    ct = 0.0
    for line in out.splitlines():
        if not line:
            continue
        if line.startswith("|"):
            try:
                ct = float(line[1:])
            except ValueError:
                ct = 0.0
        else:
            if ct > times.get(line, 0.0):
                times[line] = ct

    verdicts = read_verdicts(times, active, refs)

    _CACHE["at"] = time.time()
    _CACHE["active"] = active
    _CACHE["times"] = times
    _CACHE["refs"] = refs
    _CACHE["verdicts"] = verdicts


def read_verdicts(times: dict[str, float], active: set[str],
                  refs: dict[str, str]) -> dict[tuple[str, str], str]:
    """Find the highest-version verdict.json per (slug, role) and read its
    `status` field. Uses `git cat-file --batch` so all reads happen in one
    process — much faster than spawning git per file. Status is normalized
    to lowercase. Unknown status / parse failures are skipped (callers treat
    "no verdict" the same as "ran without verdict": green dot)."""
    import re
    latest: dict[tuple[str, str], tuple[int, str]] = {}
    pat = re.compile(r"^\.bot/([^/]+)/([^/]+)/v(\d+)/verdict\.json$")
    for path in times:
        m = pat.match(path)
        if not m:
            continue
        slug, role, v = m.group(1), m.group(2), int(m.group(3))
        if slug not in active:
            continue
        cur = latest.get((slug, role))
        if cur is None or v > cur[0]:
            latest[(slug, role)] = (v, path)

    if not latest:
        return {}

    # Build queries for cat-file --batch. Skip entries without a known ref.
    queries: list[tuple[tuple[str, str], str]] = []
    for (slug, role), (_, path) in latest.items():
        ref = refs.get(slug)
        if not ref:
            continue
        queries.append(((slug, role), f"{ref}:{path}"))

    if not queries:
        return {}

    try:
        proc = subprocess.run(
            ["git", "-C", str(REPO_ROOT), "cat-file", "--batch"],
            input="\n".join(q for _, q in queries).encode() + b"\n",
            capture_output=True, check=False, timeout=30,
        )
    except Exception:
        return {}

    # Output format per query: "<sha> blob <size>\n<size bytes>\n" or
    # "<query> missing\n". Walk byte stream and pair with queries in order.
    out = proc.stdout
    pos = 0
    results: dict[tuple[str, str], str] = {}
    for key, _ in queries:
        # Read one header line.
        nl = out.find(b"\n", pos)
        if nl < 0:
            break
        header = out[pos:nl].decode("utf-8", errors="replace")
        pos = nl + 1
        parts = header.split()
        if len(parts) >= 2 and parts[1] == "missing":
            continue
        if len(parts) < 3:
            continue
        try:
            size = int(parts[2])
        except ValueError:
            continue
        blob = out[pos:pos + size]
        pos += size + 1  # +1 for trailing newline
        try:
            data = json.loads(blob.decode("utf-8"))
            status = str(data.get("status", "")).strip().lower()
            if status:
                results[key] = status
        except (json.JSONDecodeError, UnicodeDecodeError):
            continue
    return results


def _ensure_cache() -> None:
    if time.time() - _CACHE["at"] >= _CACHE_TTL or not _CACHE["times"]:
        refresh_cache()


def active_branches() -> set[str]:
    _ensure_cache()
    return _CACHE["active"]


def git_commit_times() -> dict[str, float]:
    _ensure_cache()
    return _CACHE["times"]


def slug_refs() -> dict[str, str]:
    _ensure_cache()
    return _CACHE["refs"]


def verdicts() -> dict[tuple[str, str], str]:
    _ensure_cache()
    return _CACHE["verdicts"]


_FILE_LIMIT = 200 * 1024  # 200 KB cap for the hover preview


def get_file_payload(slug: str, path: str) -> dict:
    """Read a file from the branch's ref via `git show`. Returns
    {ok, html, kind, truncated, error}."""
    if slug not in active_branches():
        return {"ok": False, "error": "unknown branch"}
    if not path.startswith(f".bot/{slug}/"):
        return {"ok": False, "error": "path outside branch"}
    ref = slug_refs().get(slug)
    if not ref:
        return {"ok": False, "error": "no ref for branch"}
    proc = subprocess.run(
        ["git", "-C", str(REPO_ROOT), "show", f"{ref}:{path}"],
        capture_output=True, check=False, timeout=10,
    )
    if proc.returncode != 0:
        return {"ok": False, "error": "git show failed"}
    raw = proc.stdout
    truncated = len(raw) > _FILE_LIMIT
    if truncated:
        raw = raw[:_FILE_LIMIT]
    try:
        text = raw.decode("utf-8")
    except UnicodeDecodeError:
        return {"ok": True, "kind": "binary", "html": "<em>binary file</em>",
                "truncated": truncated}
    lower = path.lower()
    if lower.endswith(".json"):
        return {"ok": True, "kind": "json", "html": render_json(text),
                "truncated": truncated}
    if lower.endswith(".md") or lower.endswith(".markdown"):
        return {"ok": True, "kind": "md", "html": render_markdown(text),
                "truncated": truncated}
    return {"ok": True, "kind": "text",
            "html": f'<pre class="raw">{html_escape(text)}</pre>',
            "truncated": truncated}


def html_escape(s: str) -> str:
    return (s.replace("&", "&amp;").replace("<", "&lt;")
             .replace(">", "&gt;"))


def render_json(text: str) -> str:
    try:
        obj = json.loads(text)
        pretty = json.dumps(obj, indent=2, ensure_ascii=False)
    except json.JSONDecodeError:
        pretty = text  # show as-is if invalid
    return f'<pre class="json">{html_escape(pretty)}</pre>'


_MD_INLINE_PATTERNS = [
    # Code spans first so their contents don't get parsed as bold/italic.
    (__import__("re").compile(r"`([^`\n]+)`"),
     lambda m: f"<code>{html_escape(m.group(1))}</code>"),
    (__import__("re").compile(r"\*\*([^*\n]+)\*\*"),
     lambda m: f"<strong>{html_escape(m.group(1))}</strong>"),
    (__import__("re").compile(r"(?<![\*\w])\*([^*\n]+)\*(?!\*)"),
     lambda m: f"<em>{html_escape(m.group(1))}</em>"),
    (__import__("re").compile(r"\[([^\]]+)\]\(([^)\s]+)\)"),
     lambda m: f'<a href="{html_escape(m.group(2))}">{html_escape(m.group(1))}</a>'),
]


def render_inline(s: str) -> str:
    """Tiny inline renderer: code spans, bold, italic, links. Everything
    else is treated as plain text (escaped)."""
    import re
    tokens: list[tuple[str, str]] = []  # (kind, content) — kind=raw|html
    tokens.append(("raw", s))
    for pat, repl in _MD_INLINE_PATTERNS:
        new: list[tuple[str, str]] = []
        for kind, content in tokens:
            if kind == "html":
                new.append((kind, content))
                continue
            last = 0
            for m in pat.finditer(content):
                if m.start() > last:
                    new.append(("raw", content[last:m.start()]))
                new.append(("html", repl(m)))
                last = m.end()
            if last < len(content):
                new.append(("raw", content[last:]))
        tokens = new
    return "".join(html_escape(c) if k == "raw" else c for k, c in tokens)


def render_markdown(text: str) -> str:
    """Minimal block-level markdown renderer for hover previews.
    Handles: headings, fenced code, lists, blockquotes, hr, paragraphs."""
    import re
    lines = text.splitlines()
    out: list[str] = []
    i = 0
    n = len(lines)
    while i < n:
        ln = lines[i]
        if not ln.strip():
            i += 1
            continue
        # Fenced code
        if ln.lstrip().startswith("```"):
            i += 1
            code: list[str] = []
            while i < n and not lines[i].lstrip().startswith("```"):
                code.append(lines[i])
                i += 1
            if i < n:
                i += 1
            out.append(f'<pre class="code">{html_escape(chr(10).join(code))}</pre>')
            continue
        # Heading
        m = re.match(r"^(#{1,6})\s+(.*)$", ln)
        if m:
            level = len(m.group(1))
            out.append(f"<h{level}>{render_inline(m.group(2))}</h{level}>")
            i += 1
            continue
        # Horizontal rule
        if re.match(r"^\s*([-*_])\1{2,}\s*$", ln):
            out.append("<hr>")
            i += 1
            continue
        # List (consecutive items)
        if re.match(r"^\s*([-*+]|\d+\.)\s+", ln):
            ordered = bool(re.match(r"^\s*\d+\.\s+", ln))
            items: list[str] = []
            while i < n and re.match(r"^\s*([-*+]|\d+\.)\s+", lines[i]):
                item = re.sub(r"^\s*([-*+]|\d+\.)\s+", "", lines[i])
                items.append(f"<li>{render_inline(item)}</li>")
                i += 1
            tag = "ol" if ordered else "ul"
            out.append(f"<{tag}>{''.join(items)}</{tag}>")
            continue
        # Blockquote
        if ln.lstrip().startswith(">"):
            quoted: list[str] = []
            while i < n and lines[i].lstrip().startswith(">"):
                quoted.append(re.sub(r"^\s*>\s?", "", lines[i]))
                i += 1
            out.append(f"<blockquote>{render_inline(' '.join(quoted))}</blockquote>")
            continue
        # Paragraph (collect until blank line)
        para: list[str] = [ln]
        i += 1
        while i < n and lines[i].strip() and not re.match(
                r"^(#{1,6}\s|```|\s*([-*+]|\d+\.)\s|>)", lines[i]):
            para.append(lines[i])
            i += 1
        out.append(f"<p>{render_inline(' '.join(para))}</p>")
    return "\n".join(out)


def scan_branches() -> list[dict]:
    """Build the overview from git history, filtered to active branches.

    Filesystem is NOT scanned — a feature branch's `.bot/<slug>/` only exists
    in the working tree when that branch is checked out, so a fs-only view
    silently misses every other branch. The git log dict, by contrast, sees
    files committed on any ref."""
    active = active_branches()
    cur = current_branch()
    times = git_commit_times()
    verds = verdicts()

    grouped: dict[str, list[tuple[str, float]]] = {}
    for path, ct in times.items():
        parts = path.split("/")
        if len(parts) < 3 or parts[0] != ".bot":
            continue
        slug = parts[1]
        if slug in NON_BRANCH or slug not in active:
            continue
        grouped.setdefault(slug, []).append((path, ct))

    branches = []
    for slug, files in grouped.items():
        roles: dict[str, dict] = {}
        other: dict[str, dict] = {}
        root_files: list[dict] = []
        for path, ct in files:
            parts = path.split("/")
            # parts: .bot / slug / (role | filename) / ...
            if len(parts) == 3:
                root_files.append({"name": parts[2], "mtime": ct})
            else:
                role = parts[2]
                rel = "/".join(parts[3:])
                bucket = roles if role in KNOWN_ROLES else other
                entry = bucket.setdefault(role, {"mtime": 0.0, "files": []})
                entry["files"].append({"path": rel, "mtime": ct})
                if ct > entry["mtime"]:
                    entry["mtime"] = ct
        for bucket in (roles, other):
            for entry in bucket.values():
                entry["files"].sort(key=lambda f: f["path"])
        # Attach verdict status (if any) per role. "pass" → ok,
        # anything else → "fail" for color purposes. No verdict → null
        # (frontend treats as ok since the role ran but didn't claim a status).
        for role_name, entry in roles.items():
            status = verds.get((slug, role_name))
            if status:
                entry["status"] = "pass" if status == "pass" else "fail"
                entry["statusRaw"] = status
        root_files.sort(key=lambda f: f["name"])
        last = max((ct for _, ct in files), default=0.0)
        branches.append({
            "name": slug,
            "lastActivity": last,
            "isCurrent": slug == cur,
            "roles": roles,
            "other": other,
            "rootFiles": root_files,
        })
    branches.sort(key=lambda b: b["lastActivity"], reverse=True)
    return branches


INDEX_HTML = r"""<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>Branches</title>
<style>
  :root {
    --bg: #1e1e1e;
    --panel: #252526;
    --panel2: #2d2d30;
    --border: #3e3e42;
    --text: #d4d4d4;
    --muted: #858585;
    --accent: #569cd6;
    --done: #6a9955;
    --pending: #555;
    --current: #c586c0;
    --warn: #ce9178;
  }
  * { box-sizing: border-box; }
  body {
    margin: 0;
    font: 13px/1.4 -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
    background: var(--bg);
    color: var(--text);
    height: 100vh;
    display: flex;
  }
  #nav {
    width: 320px;
    min-width: 320px;
    background: var(--panel);
    border-right: 1px solid var(--border);
    overflow-y: auto;
  }
  #nav header {
    padding: 12px 14px;
    border-bottom: 1px solid var(--border);
    background: var(--panel2);
    font-weight: 600;
    display: flex;
    justify-content: space-between;
    align-items: center;
  }
  #nav header .count { color: var(--muted); font-weight: normal; }
  .branch {
    padding: 8px 14px;
    border-bottom: 1px solid var(--border);
    cursor: pointer;
  }
  .branch:hover { background: var(--panel2); }
  .branch.active { background: #094771; }
  .branch.current { border-left: 3px solid var(--current); padding-left: 11px; }
  .branch .name { font-weight: 500; word-break: break-all; }
  .branch .meta { color: var(--muted); font-size: 11px; margin-top: 2px; display: flex; gap: 8px; flex-wrap: wrap; }
  .branch .roles { margin-top: 4px; display: flex; gap: 2px; }
  .branch .roles .spacer { flex: 1; min-width: 8px; }
  .role-dot {
    width: 10px; height: 10px; border-radius: 2px;
    background: var(--pending);
  }
  .role-dot.done { background: var(--done); }
  .role-dot.fail { background: #d7ba7d; }  /* yellow — ran but verdict failed */
  #pane {
    flex: 1;
    padding: 20px 28px;
    overflow-y: auto;
    min-width: 0;  /* let the flex layout shrink this */
  }
  /* Always-on preview pane on the right; updates as files are hovered.
     No floating popup means no overlap with the file list — the cursor
     can scan the file rows without losing its target. */
  #preview {
    width: 560px;
    min-width: 560px;
    border-left: 1px solid var(--border);
    background: var(--panel);
    overflow-y: auto;
    padding: 14px 18px;
    font-size: 12px;
    line-height: 1.5;
  }
  #preview .header {
    color: var(--muted);
    font-family: ui-monospace, monospace;
    font-size: 11px;
    margin-bottom: 8px;
    padding-bottom: 6px;
    border-bottom: 1px solid var(--border);
    display: flex;
    justify-content: space-between;
    word-break: break-all;
    gap: 8px;
  }
  #preview .placeholder { color: var(--muted); font-style: italic; }
  #preview pre {
    margin: 0;
    white-space: pre-wrap;
    word-break: break-word;
    font-family: ui-monospace, monospace;
  }
  #preview pre.json { color: #ce9178; }
  #preview pre.code { background: var(--bg); padding: 8px; border-radius: 3px; }
  #preview h1, #preview h2, #preview h3,
  #preview h4, #preview h5, #preview h6 {
    margin: 12px 0 6px; line-height: 1.25;
  }
  #preview h1 { font-size: 16px; }
  #preview h2 { font-size: 14px; }
  #preview h3 { font-size: 13px; }
  #preview p { margin: 6px 0; }
  #preview ul, #preview ol { margin: 6px 0; padding-left: 22px; }
  #preview li { margin: 2px 0; }
  #preview code {
    background: var(--bg); padding: 1px 4px; border-radius: 2px;
    font-family: ui-monospace, monospace; font-size: 11px;
  }
  #preview blockquote {
    border-left: 3px solid var(--border);
    padding-left: 10px;
    color: var(--muted);
    margin: 6px 0;
  }
  #preview .trunc {
    margin-top: 8px;
    padding: 6px 8px;
    background: var(--bg);
    border-radius: 3px;
    color: var(--warn);
  }
  #pane .empty {
    color: var(--muted);
    text-align: center;
    margin-top: 30vh;
  }
  #pane h1 {
    font-size: 20px;
    margin: 0 0 4px;
    word-break: break-all;
    display: inline-flex;
    align-items: center;
    gap: 8px;
  }
  #pane .copy-btn {
    background: var(--panel2);
    color: var(--muted);
    border: 1px solid var(--border);
    border-radius: 3px;
    font: 11px ui-monospace, monospace;
    padding: 2px 6px;
    cursor: pointer;
  }
  #pane .copy-btn:hover { color: var(--text); }
  #pane .copy-btn.copied { color: var(--done); border-color: var(--done); }
  #pane .subtitle { color: var(--muted); margin-bottom: 24px; display: flex; gap: 12px; flex-wrap: wrap; }
  #pane .subtitle .pill {
    padding: 1px 8px; border-radius: 10px; background: var(--panel2);
    border: 1px solid var(--border);
  }
  #pane .subtitle .pill.current { color: var(--current); border-color: var(--current); }
  .timeline {
    display: flex;
    flex-direction: column;
    gap: 8px;
    margin-bottom: 24px;
  }
  .stage {
    display: grid;
    grid-template-columns: 140px 24px 1fr 160px;
    align-items: center;
    gap: 10px;
    padding: 8px 12px;
    background: var(--panel);
    border: 1px solid var(--border);
    border-radius: 4px;
  }
  .muted-note { color: var(--muted); font-style: italic; padding: 8px 0 16px; }
  .missing {
    color: var(--muted);
    font-size: 12px;
    margin-top: 8px;
    padding: 6px 12px;
    background: var(--panel);
    border: 1px dashed var(--border);
    border-radius: 4px;
  }
  .stage .name { font-weight: 500; }
  .stage .icon { text-align: center; font-size: 14px; }
  .stage .icon.done { color: var(--done); }
  .stage .icon.fail { color: #d7ba7d; }
  .opt-divider {
    color: var(--muted);
    font-size: 11px;
    text-transform: uppercase;
    letter-spacing: 0.06em;
    padding: 6px 0 2px;
    border-top: 1px dashed var(--border);
    margin-top: 4px;
  }
  .status-pill {
    display: inline-block;
    font-size: 10px;
    padding: 1px 6px;
    margin-left: 8px;
    border-radius: 8px;
    text-transform: uppercase;
    letter-spacing: 0.05em;
  }
  .status-pill.pass { background: rgba(106, 153, 85, 0.18); color: var(--done); }
  .status-pill.fail { background: rgba(215, 186, 125, 0.18); color: #d7ba7d; }
  .stage .files { color: var(--muted); font-size: 12px; }
  .stage .time { color: var(--muted); font-size: 12px; text-align: right; }
  .stage details summary { cursor: pointer; color: var(--muted); }
  .stage details summary:hover { color: var(--text); }
  .stage .filelist { margin-top: 6px; padding-left: 12px; font-family: ui-monospace, monospace; font-size: 11px; }
  .stage .filelist li { color: var(--muted); }
  h2.section { font-size: 13px; text-transform: uppercase; color: var(--muted); margin: 20px 0 8px; letter-spacing: 0.05em; }
  ul.flat { list-style: none; padding: 0; margin: 0; font-family: ui-monospace, monospace; font-size: 12px; }
  ul.flat li { padding: 4px 0; border-bottom: 1px solid var(--border); display: flex; justify-content: space-between; }
  ul.flat li .ts { color: var(--muted); }
  .filter {
    padding: 8px 12px;
    border-bottom: 1px solid var(--border);
  }
  .stage .filelist li[data-branch] { cursor: pointer; }
  .stage .filelist li[data-branch]:hover { color: var(--text); background: var(--panel2); }
  .stage .filelist li[data-branch].selected { color: var(--text); background: #094771; }
  ul.flat li[data-branch] { cursor: pointer; }
  ul.flat li[data-branch]:hover { background: var(--panel2); }
  ul.flat li[data-branch].selected { background: #094771; color: var(--text); }
  .filter input {
    width: 100%;
    padding: 6px 8px;
    background: var(--bg);
    color: var(--text);
    border: 1px solid var(--border);
    border-radius: 3px;
    font: inherit;
  }
</style>
</head>
<body>
<div id="nav">
  <header>
    <span>Branches</span>
    <span class="count" id="count"></span>
  </header>
  <div class="filter"><input id="filter" placeholder="filter..." autofocus></div>
  <div id="branchList"></div>
</div>
<div id="pane"><div class="empty">Select a branch on the left.</div></div>
<div id="preview"><div class="placeholder">Hover a file to preview.</div></div>

<script>
const PIPELINE = ["architect","test-designer","coder","codeanalyzer","tester","security","auditor"];
const OPTIONAL = ["builder","docs"];
const EXTRA_ROLES = ["handoff","learnings","scaffolder"];
let BRANCHES = [];
let SELECTED = null;
const FILE_CACHE = new Map();  // "branch::path" -> rendered payload

function fmtTime(epoch) {
  if (!epoch) return "";
  const d = new Date(epoch * 1000);
  const now = new Date();
  const diff = (now - d) / 1000;
  if (diff < 60) return Math.floor(diff) + "s ago";
  if (diff < 3600) return Math.floor(diff / 60) + "m ago";
  if (diff < 86400) return Math.floor(diff / 3600) + "h ago";
  if (diff < 86400 * 30) return Math.floor(diff / 86400) + "d ago";
  return d.toISOString().slice(0, 10);
}

function fmtTimeAbs(epoch) {
  if (!epoch) return "";
  return new Date(epoch * 1000).toISOString().replace("T", " ").slice(0, 16);
}

function renderNav(filter) {
  const list = document.getElementById("branchList");
  const f = (filter || "").toLowerCase();
  const visible = BRANCHES.filter(b => !f || b.name.toLowerCase().includes(f));
  document.getElementById("count").textContent = visible.length + " / " + BRANCHES.length;
  list.innerHTML = "";
  function dot(role, r) {
    if (!r) return `<div class="role-dot" title="${role}: not yet run"></div>`;
    const failed = r.status === "fail";
    const cls = "role-dot " + (failed ? "fail" : "done");
    const tip = role + (r.statusRaw ? ": " + r.statusRaw : ": ran");
    return `<div class="${cls}" title="${tip}"></div>`;
  }
  for (const b of visible) {
    const div = document.createElement("div");
    div.className = "branch" + (b.isCurrent ? " current" : "") + (SELECTED === b.name ? " active" : "");
    const main = PIPELINE.map(r => dot(r, b.roles[r])).join("");
    const opt = OPTIONAL.map(r => dot(r, b.roles[r])).join("");
    div.innerHTML = `
      <div class="name">${b.name}</div>
      <div class="meta"><span>${fmtTime(b.lastActivity)}</span></div>
      <div class="roles">${main}<div class="spacer"></div>${opt}</div>
    `;
    div.onclick = () => { SELECTED = b.name; renderNav(filter); renderPane(b); };
    list.appendChild(div);
  }
}

function renderPane(b) {
  const pane = document.getElementById("pane");
  const pills = [];
  if (b.isCurrent) pills.push('<span class="pill current">current branch</span>');
  pills.push(`<span class="pill">last activity ${fmtTime(b.lastActivity)}</span>`);

  // Show only stages that actually ran. Mandatory stages are listed in
  // PIPELINE order; optional ones (builder, docs) are listed below a
  // visual separator and never counted as "missing".
  function stageRow(role) {
    const r = b.roles[role];
    if (!r) return "";
    const failed = r.status === "fail";
    const iconCls = failed ? "icon fail" : "icon done";
    const iconChar = failed ? "▲" : "●";
    const statusBadge = r.statusRaw
      ? `<span class="status-pill ${failed ? "fail" : "pass"}">${r.statusRaw}</span>`
      : "";
    const fileList = r.files.length
      ? `<details><summary>${r.files.length} file${r.files.length === 1 ? "" : "s"}${statusBadge}</summary><ul class="filelist">${r.files.map(f => fileLi(b.name, `.bot/${b.name}/${role}/${f.path}`, f.path, f.mtime)).join("")}</ul></details>`
      : `<span class="files">— ${statusBadge}</span>`;
    return `
      <div class="stage">
        <div class="name">${role}</div>
        <div class="${iconCls}">${iconChar}</div>
        <div>${fileList}</div>
        <div class="time">${fmtTime(r.mtime)}</div>
      </div>
    `;
  }
  const mainPopulated = PIPELINE.filter(role => b.roles[role]);
  const optPopulated = OPTIONAL.filter(role => b.roles[role]);
  const mainStages = mainPopulated.map(stageRow).join("");
  const optStages = optPopulated.map(stageRow).join("");
  const missingStages = PIPELINE.filter(role => !b.roles[role]);
  const pipelineBlock = (mainPopulated.length || optPopulated.length)
    ? `<h2 class="section">Pipeline</h2><div class="timeline">${mainStages}${
        optStages ? `<div class="opt-divider">optional</div>${optStages}` : ""
      }</div>`
    : `<h2 class="section">Pipeline</h2><div class="muted-note">No pipeline runs yet.</div>`;
  const missingBlock = missingStages.length
    ? `<div class="missing">Not yet run: ${missingStages.join(", ")}</div>`
    : "";

  const otherRoles = Object.entries(b.other || {});
  const otherSection = otherRoles.length ? `
    <h2 class="section">Other subdirs</h2>
    <div class="timeline">
      ${otherRoles.map(([name, r]) => `
        <div class="stage">
          <div class="name">${name}</div>
          <div class="icon done">●</div>
          <div><details><summary>${r.files.length} file${r.files.length === 1 ? "" : "s"}</summary><ul class="filelist">${r.files.map(f => fileLi(b.name, `.bot/${b.name}/${name}/${f.path}`, f.path, f.mtime)).join("")}</ul></details></div>
          <div class="time">${fmtTime(r.mtime)}</div>
        </div>
      `).join("")}
    </div>
  ` : "";

  const rootSection = (b.rootFiles || []).length ? `
    <h2 class="section">Root files</h2>
    <ul class="flat">
      ${b.rootFiles.map(f => `<li data-branch="${b.name}" data-path=".bot/${b.name}/${f.name}"><span>${f.name}</span><span class="ts">${fmtTimeAbs(f.mtime)}</span></li>`).join("")}
    </ul>
  ` : "";

  pane.innerHTML = `
    <h1>${b.name}<button class="copy-btn" onclick="copyBranchName(event, '${b.name}')" title="Copy branch name">copy</button></h1>
    <div class="subtitle">${pills.join("")}</div>
    ${pipelineBlock}
    ${missingBlock}
    ${otherSection}
    ${rootSection}
  `;
}

function fileLi(branch, fullPath, displayPath, mtime) {
  return `<li data-branch="${branch}" data-path="${fullPath}">${displayPath}<span style="float:right">${fmtTime(mtime)}</span></li>`;
}

function copyBranchName(e, name) {
  e.stopPropagation();
  navigator.clipboard.writeText(name).then(() => {
    const btn = e.target;
    const orig = btn.textContent;
    btn.textContent = "copied";
    btn.classList.add("copied");
    setTimeout(() => {
      btn.textContent = orig;
      btn.classList.remove("copied");
    }, 1200);
  });
}

// File preview pane (right side, always visible) ---------------------
//
// Clicking a file row loads its rendered content into the right pane.
// Hover was tried but produced no clean UX — the cursor needs to scan
// rows without committing to a preview, and a click gives that.
const preview = document.getElementById("preview");
let previewGen = 0;
let activeLi = null;

async function loadPreview(li) {
  if (activeLi) activeLi.classList.remove("selected");
  activeLi = li;
  li.classList.add("selected");
  const branch = li.dataset.branch;
  const path = li.dataset.path;
  const key = branch + "::" + path;
  const gen = ++previewGen;
  let payload = FILE_CACHE.get(key);
  if (!payload) {
    preview.innerHTML = `<div class="header"><span>${path}</span><span>loading...</span></div>`;
    try {
      const r = await fetch(`/api/file?branch=${encodeURIComponent(branch)}&path=${encodeURIComponent(path)}`);
      payload = await r.json();
      FILE_CACHE.set(key, payload);
    } catch (err) {
      payload = { ok: false, error: String(err) };
    }
    if (gen !== previewGen) return;
  }
  const body = payload.ok
    ? payload.html + (payload.truncated ? '<div class="trunc">(preview truncated)</div>' : "")
    : `<em>${payload.error || "failed to load"}</em>`;
  preview.innerHTML = `<div class="header"><span>${path}</span><span>${payload.kind || ""}</span></div>${body}`;
  preview.scrollTop = 0;
}

document.addEventListener("click", e => {
  const li = e.target.closest("li[data-branch][data-path]");
  if (!li) return;
  e.preventDefault();  // don't toggle the surrounding <details>
  loadPreview(li);
});

async function load() {
  const res = await fetch("/api/branches");
  BRANCHES = await res.json();
  renderNav("");
}

document.getElementById("filter").addEventListener("input", e => renderNav(e.target.value));
load();
</script>
</body>
</html>
"""


class Handler(http.server.BaseHTTPRequestHandler):
    def log_message(self, fmt, *args):
        return  # quiet

    def do_GET(self):
        if self.path == "/" or self.path == "/index.html":
            self._send(200, "text/html; charset=utf-8", INDEX_HTML.encode("utf-8"))
            return
        if self.path == "/api/branches":
            data = json.dumps(scan_branches()).encode("utf-8")
            self._send(200, "application/json", data)
            return
        if self.path.startswith("/api/file?"):
            import urllib.parse
            qs = urllib.parse.parse_qs(self.path.split("?", 1)[1])
            slug = (qs.get("branch") or [""])[0]
            path = (qs.get("path") or [""])[0]
            payload = get_file_payload(slug, path)
            self._send(200, "application/json",
                       json.dumps(payload).encode("utf-8"))
            return
        self._send(404, "text/plain", b"not found")

    def _send(self, code, ctype, body):
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(body)


def main():
    httpd = http.server.ThreadingHTTPServer(("0.0.0.0", PORT), Handler)
    print(f"branches: http://localhost:{PORT}  (repo {REPO_ROOT})")
    httpd.serve_forever()


if __name__ == "__main__":
    main()
