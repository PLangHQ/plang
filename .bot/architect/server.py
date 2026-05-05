#!/usr/bin/env python3
"""
Architect review server — view current-branch architect output, comment per line.

Run:   python3 .bot/architect/server.py
Open:  http://localhost:8081

Auto-detects the current git branch and serves files under
.bot/<branch>/architect/  (all *.md, recursive).

Renders Markdown -> HTML in the browser, but every block (and every list
item) carries the SOURCE line number so comments anchor to the exact
line of the .md file. Comments are saved to
.bot/<branch>/architect/comments.json so they live with the branch.
"""

import http.server
import json
import re
import subprocess
import urllib.parse
from html import escape
from pathlib import Path

PORT = 8081
REPO_ROOT = Path(__file__).resolve().parents[2]


def current_branch() -> str:
    out = subprocess.check_output(
        ["git", "-C", str(REPO_ROOT), "branch", "--show-current"]
    )
    return out.decode().strip()


def architect_dir() -> Path:
    branch = current_branch().replace("/", "-")
    return REPO_ROOT / ".bot" / branch / "architect"


def list_files() -> list[str]:
    base = architect_dir()
    if not base.exists():
        return []
    files = [str(p.relative_to(base)) for p in sorted(base.rglob("*.md"))]
    def sort_key(rel: str):
        parts = rel.split("/")
        if len(parts) == 1:
            return (0, 0, rel)
        v = parts[0]
        n = int(v[1:]) if v.startswith("v") and v[1:].isdigit() else 999
        return (1, n, rel)
    files.sort(key=sort_key)
    return files


def review_requested_mtime() -> float:
    """Epoch seconds of the last 'Send to Architect' click, or 0 if never."""
    marker = architect_dir() / "review-requested.json"
    if not marker.exists():
        return 0.0
    return marker.stat().st_mtime


def files_modified_since_review() -> list[str]:
    base = architect_dir()
    cutoff = review_requested_mtime()
    if cutoff == 0 or not base.exists():
        return []
    out = []
    for rel in list_files():
        p = base / rel
        if p.exists() and p.stat().st_mtime > cutoff:
            out.append(rel)
    return out


def comments_path() -> Path:
    return architect_dir() / "comments.json"


def load_comments() -> dict:
    p = comments_path()
    if not p.exists():
        return {}
    try:
        return json.loads(p.read_text())
    except Exception:
        return {}


def save_comments(data: dict) -> None:
    p = comments_path()
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(json.dumps(data, indent=2, ensure_ascii=False))


def load_comments_normalized() -> dict:
    return normalize_comments(load_comments())


def normalize_comments(data: dict) -> dict:
    """Backfill author/status/parent_id defaults on legacy entries."""
    for f, arr in data.items():
        for c in arr:
            c.setdefault("author", "user")
            c.setdefault("status", "open")
            c.setdefault("parent_id", None)
    return data


# --- Minimal Markdown renderer that tracks source line numbers ---

INLINE_CODE = re.compile(r"`([^`]+)`")
BOLD = re.compile(r"\*\*([^*]+)\*\*")
ITALIC = re.compile(r"(?<!\*)\*([^*\n]+)\*(?!\*)")
LINK = re.compile(r"\[([^\]]+)\]\(([^)]+)\)")


def render_inline(text: str) -> str:
    # Escape first, then re-introduce inline markup.
    s = escape(text)
    placeholders: list[str] = []

    def stash(html: str) -> str:
        placeholders.append(html)
        return f"\x00{len(placeholders)-1}\x00"

    s = INLINE_CODE.sub(lambda m: stash(f"<code>{m.group(1)}</code>"), s)
    s = LINK.sub(lambda m: stash(
        f'<a href="{m.group(2)}">{m.group(1)}</a>'
    ), s)
    s = BOLD.sub(r"<strong>\1</strong>", s)
    s = ITALIC.sub(r"<em>\1</em>", s)
    s = re.sub(r"\x00(\d+)\x00", lambda m: placeholders[int(m.group(1))], s)
    return s


def is_list_item(s: str) -> bool:
    return bool(re.match(r"^\s*([-*+]|\d+\.)\s+", s))


TABLE_SEP = re.compile(r"^\s*\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)+\|?\s*$")


def split_row(s: str) -> list[str]:
    s = s.strip()
    if s.startswith("|"):
        s = s[1:]
    if s.endswith("|"):
        s = s[:-1]
    return [c.strip() for c in s.split("|")]


def is_table_start(lines: list[str], i: int) -> bool:
    if i + 1 >= len(lines):
        return False
    if "|" not in lines[i]:
        return False
    return bool(TABLE_SEP.match(lines[i + 1]))


def render_markdown(text: str) -> list[dict]:
    """Return a list of blocks: {start, end, html}.

    `start`/`end` are 1-based source line numbers (inclusive).
    For lists, individual <li> elements carry data-line on the rendered HTML
    so comments can anchor to specific items.
    """
    lines = text.splitlines()
    blocks: list[dict] = []
    i = 0
    n = len(lines)
    while i < n:
        ln = lines[i]
        if not ln.strip():
            i += 1
            continue
        start = i + 1

        # Fenced code block
        if ln.lstrip().startswith("```"):
            i += 1
            code = []
            while i < n and not lines[i].lstrip().startswith("```"):
                code.append(lines[i])
                i += 1
            if i < n:
                i += 1  # closing ```
            html = f'<pre><code>{escape(chr(10).join(code))}</code></pre>'
            blocks.append({"start": start, "end": i, "html": html})
            continue

        # ATX heading
        m = re.match(r"^(#{1,6})\s+(.*)$", ln)
        if m:
            level = len(m.group(1))
            html = f"<h{level}>{render_inline(m.group(2))}</h{level}>"
            blocks.append({"start": start, "end": start, "html": html})
            i += 1
            continue

        # Horizontal rule
        if re.match(r"^\s*([-*_])\1{2,}\s*$", ln):
            blocks.append({"start": start, "end": start, "html": "<hr>"})
            i += 1
            continue

        # GFM table: header row, separator, then body rows
        if is_table_start(lines, i):
            tstart = start
            header = split_row(lines[i])
            i += 2  # skip header + separator
            rows_html = [
                "<thead><tr data-line=\"" + str(tstart) + "\">"
                + "".join(f"<th>{render_inline(c)}</th>" for c in header)
                + "</tr></thead>"
            ]
            body_rows = []
            while i < n and "|" in lines[i] and lines[i].strip():
                row_line = i + 1
                cells = split_row(lines[i])
                # pad/truncate to header width
                if len(cells) < len(header):
                    cells += [""] * (len(header) - len(cells))
                else:
                    cells = cells[: len(header)]
                body_rows.append(
                    f'<tr data-line="{row_line}">'
                    + "".join(f"<td>{render_inline(c)}</td>" for c in cells)
                    + "</tr>"
                )
                i += 1
            if body_rows:
                rows_html.append("<tbody>" + "".join(body_rows) + "</tbody>")
            html = "<table>" + "".join(rows_html) + "</table>"
            blocks.append({"start": tstart, "end": i, "html": html})
            continue

        # Lists (unordered or ordered) — group consecutive items
        if is_list_item(ln):
            ordered = bool(re.match(r"^\s*\d+\.\s+", ln))
            items_html = []
            list_start = start
            while i < n and is_list_item(lines[i]):
                item_line = i + 1
                item_text = re.sub(r"^\s*([-*+]|\d+\.)\s+", "", lines[i])
                items_html.append(
                    f'<li data-line="{item_line}">{render_inline(item_text)}</li>'
                )
                i += 1
            tag = "ol" if ordered else "ul"
            html = f"<{tag}>{''.join(items_html)}</{tag}>"
            blocks.append({"start": list_start, "end": i, "html": html})
            continue

        # Blockquote
        if ln.lstrip().startswith(">"):
            qlines = []
            while i < n and lines[i].lstrip().startswith(">"):
                qlines.append(re.sub(r"^\s*>\s?", "", lines[i]))
                i += 1
            html = f"<blockquote>{render_inline(' '.join(qlines))}</blockquote>"
            blocks.append({"start": start, "end": i, "html": html})
            continue

        # Paragraph: collect until blank line or new block-start
        para = [ln]
        i += 1
        while (
            i < n
            and lines[i].strip()
            and not lines[i].lstrip().startswith("```")
            and not re.match(r"^#{1,6}\s+", lines[i])
            and not is_list_item(lines[i])
            and not lines[i].lstrip().startswith(">")
            and not re.match(r"^\s*([-*_])\1{2,}\s*$", lines[i])
        ):
            para.append(lines[i])
            i += 1
        html = f"<p>{render_inline(' '.join(para))}</p>"
        blocks.append({"start": start, "end": i, "html": html})

    return blocks


INDEX_HTML = r"""<!doctype html>
<html><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
<title>Architect Review — {branch}</title>
<style>
  * { box-sizing: border-box; }
  body { margin:0; font-family: -apple-system, system-ui, sans-serif; display:flex; height:100vh; background:#1e1e1e; color:#d4d4d4; }
  #sidebar { width: 280px; background:#252526; border-right:1px solid #333; overflow-y:auto; padding:12px; flex-shrink:0; }
  #sidebar h2 { font-size:13px; text-transform:uppercase; color:#888; margin:0 0 8px 0; }
  #sidebar .branch { font-size:11px; color:#6a9955; margin-bottom:12px; word-break:break-all; }
  #sidebar ul { list-style:none; padding:0; margin:0; }
  #sidebar li { padding:5px 8px; cursor:pointer; border-radius:3px; font-size:13px; font-family:monospace; }
  #sidebar li:hover { background:#2a2d2e; }
  #sidebar li.active { background:#094771; color:#fff; }
  #sidebar li .badge { float:right; background:#f48771; color:#000; padding:0 6px; border-radius:8px; font-size:10px; }
  #sidebar li .modified { display:inline-block; width:7px; height:7px; border-radius:50%; background:#dcdcaa; margin-right:6px; vertical-align:middle; box-shadow:0 0 4px rgba(220,220,170,0.6); }
  #sidebar li .modified[title] { cursor:help; }
  #main { flex:1; overflow-y:auto; }
  #header { padding:12px 20px; background:#2d2d30; border-bottom:1px solid #333; position:sticky; top:0; z-index:10; display:flex; align-items:center; gap:12px; }
  #header h1 { margin:0; font-size:14px; font-family:monospace; color:#9cdcfe; flex:1; }
  #header .toggle { font-size:12px; color:#888; cursor:pointer; user-select:none; }
  #header .toggle:hover { color:#fff; }
  #content { padding:20px 40px 80px; max-width: 980px; }
  #content.raw { font-family: 'SF Mono', Consolas, monospace; font-size:13px; }
  /* Markdown styling */
  .md h1, .md h2, .md h3, .md h4 { color:#e8e8e8; border-bottom:1px solid #333; padding-bottom:4px; margin: 18px 0 10px; }
  .md h1 { font-size: 22px; } .md h2 { font-size: 18px; } .md h3 { font-size: 15px; } .md h4 { font-size: 14px; }
  .md p { line-height:1.6; }
  .md a { color: #569cd6; }
  .md code { background:#2d2d30; padding:1px 5px; border-radius:3px; font-family:'SF Mono',Consolas,monospace; font-size:0.92em; color:#ce9178; }
  .md pre { background:#0e0e0e; border:1px solid #333; padding:10px 14px; border-radius:4px; overflow-x:auto; }
  .md pre code { background:transparent; padding:0; color:#d4d4d4; font-size:12px; }
  .md blockquote { border-left:3px solid #569cd6; margin: 10px 0; padding: 4px 12px; color:#bbb; background:#252b32; }
  .md ul, .md ol { padding-left: 26px; line-height:1.6; }
  .md hr { border:0; border-top:1px solid #333; margin: 16px 0; }
  .md table { border-collapse: collapse; margin: 10px 0; font-size: 13px; }
  .md th, .md td { border:1px solid #444; padding: 6px 10px; text-align:left; vertical-align: top; }
  .md th { background:#2d2d30; color:#e8e8e8; }
  .md tr { position:relative; }
  .md tbody tr:nth-child(even) { background:#23272b; }
  .md tr .row-gutter { display:none; position:absolute; left:-40px; top:6px; font-family:monospace; font-size:11px; color:#666; cursor:pointer; padding:0 4px; border-radius:3px; }
  .md tr:hover > .row-gutter { display:inline; }
  .md tr .row-gutter:hover { background:#094771; color:#fff; }
  .md tr .row-gutter.has-comment { display:inline; color:#f48771; font-weight:bold; }
  /* Block hover & comment markers */
  .block, .md li { position:relative; }
  .block { padding: 2px 8px 2px 56px; border-radius:3px; }
  .block:hover { background:#252b32; }
  .block:hover > .gutter { color:#999; }
  .gutter { position:absolute; left:6px; top:4px; width:42px; text-align:right; font-family:monospace; font-size:11px; color:#3a3a3a; user-select:none; cursor:pointer; padding:2px 4px; border-radius:3px; }
  .gutter:hover { background:#094771; color:#fff !important; }
  .gutter.has-comment { color:#f48771 !important; font-weight:bold; }
  .md li .li-gutter { display:none; position:absolute; left:-40px; top:0; font-family:monospace; font-size:11px; color:#666; cursor:pointer; padding:0 4px; border-radius:3px; }
  .md li:hover > .li-gutter { display:inline; }
  .md li .li-gutter:hover { background:#094771; color:#fff; }
  .md li .li-gutter.has-comment { display:inline; color:#f48771; font-weight:bold; }
  /* Raw view */
  .line-row { display:flex; line-height:1.5; }
  .line-row:hover { background:#2a2d2e; }
  .ln { width:50px; text-align:right; padding-right:12px; color:#555; user-select:none; cursor:pointer; flex-shrink:0; }
  .ln:hover { color:#fff !important; background:#094771; }
  .ln.has-comment { color:#f48771; font-weight:bold; }
  .lc { white-space:pre-wrap; word-break:break-word; flex:1; padding-right:12px; font-family:'SF Mono',Consolas,monospace; }
  /* Comments */
  .comment-block { margin: 6px 0 6px 56px; padding:10px 12px; background:#2d3a4e; border-left:3px solid #569cd6; border-radius:3px; font-family:-apple-system,system-ui,sans-serif; font-size:13px; }
  .comment-block.author-architect { background:#3d352d; border-left-color:#dcdcaa; }
  .comment-block.status-resolved { opacity:0.55; border-left-color:#6a9955 !important; background:#1f261f !important; }
  .comment-block.status-resolved .body { text-decoration: line-through; color:#888; }
  .comment-block.status-disagreed { border-left-color:#f48771 !important; background:#3a2d2d !important; }
  .comment-block.is-reply { margin-left: 80px; }
  .comment-block .meta { color:#888; font-size:11px; margin-bottom:4px; display:flex; gap:8px; align-items:center; flex-wrap:wrap; }
  .comment-block .meta .who { font-weight:bold; }
  .comment-block.author-user .meta .who { color:#9cdcfe; }
  .comment-block.author-architect .meta .who { color:#dcdcaa; }
  .comment-block .meta .badge { padding:1px 6px; border-radius:8px; font-size:10px; background:#444; color:#fff; }
  .comment-block .meta .badge.resolved { background:#6a9955; }
  .comment-block .meta .badge.disagreed { background:#f48771; color:#000; }
  .comment-block .body { white-space:pre-wrap; word-wrap: break-word; }
  .comment-block .actions { margin-top: 8px; display:flex; gap:6px; flex-wrap:wrap; }
  .comment-block .actions button { background:#3a3d41; color:#ccc; border:0; padding:4px 10px; border-radius:3px; cursor:pointer; font-size:11px; }
  .comment-block .actions button.resolve { background:#3a5a3a; color:#cfe6cf; }
  .comment-block .actions button.disagree { background:#5a3a3a; color:#f4cfcf; }
  .comment-block .actions button.reply { background:#3a4a5a; color:#cfdce6; }
  .comment-block .actions button.del { background:transparent; color:#f48771; padding:4px 6px; }
  .comment-block .actions button:hover { filter: brightness(1.3); }
  .composer { margin: 6px 0 6px 56px; padding:10px; background:#252526; border:1px solid #569cd6; border-radius:3px; }
  .composer textarea { width:100%; min-height:70px; background:#1e1e1e; color:#d4d4d4; border:1px solid #444; padding:6px; font-family:inherit; font-size:13px; resize:vertical; }
  .composer .row { margin-top:6px; display:flex; gap:8px; align-items:center; }
  .composer button { background:#0e639c; color:#fff; border:0; padding:6px 14px; cursor:pointer; border-radius:2px; font-size:13px; }
  .composer button.cancel { background:#3a3d41; }
  .composer .target { color:#888; font-size:11px; flex:1; }
  .empty { padding:40px; color:#888; text-align:center; }
  #send-architect { background:#0e639c; color:#fff; border:0; padding:6px 12px; cursor:pointer; border-radius:3px; font-size:12px; }
  #send-architect:hover { background:#1177bb; }
  #send-architect:disabled { background:#3a3d41; cursor:not-allowed; }
  #toast { position:fixed; bottom:20px; left:50%; transform:translateX(-50%); background:#2d3a4e; color:#fff; padding:14px 18px; border-radius:6px; border:1px solid #569cd6; box-shadow:0 4px 16px rgba(0,0,0,0.4); display:none; z-index:50; max-width:90vw; font-size:13px; line-height:1.5; }
  #toast.show { display:block; }
  #toast .prompt { background:#1e1e1e; padding:8px; border-radius:3px; margin-top:8px; font-family:monospace; font-size:12px; word-break:break-word; white-space:pre-wrap; }
  #toast .close { float:right; cursor:pointer; color:#888; margin-left:12px; }
  /* Hamburger — hidden on desktop */
  #hamburger { display:none; background:transparent; border:0; color:#d4d4d4; font-size:22px; padding:4px 10px; cursor:pointer; }
  #scrim { display:none; position:fixed; inset:0; background:rgba(0,0,0,0.5); z-index:20; }
  /* Mobile */
  @media (max-width: 760px) {
    body { font-size: 15px; }
    #hamburger { display:inline-block; }
    #sidebar {
      position: fixed; top:0; left:0; bottom:0; width: 78%; max-width: 320px;
      transform: translateX(-100%); transition: transform 0.2s ease;
      z-index: 30; box-shadow: 2px 0 12px rgba(0,0,0,0.4);
    }
    body.drawer-open #sidebar { transform: translateX(0); }
    body.drawer-open #scrim { display:block; }
    #sidebar li { padding: 10px 8px; font-size: 14px; }
    #main { width: 100%; }
    #header { padding: 10px 12px; }
    #header h1 { font-size: 13px; }
    #content { padding: 12px 14px 80px; }
    /* Bigger tap targets for gutters */
    .block { padding: 6px 8px 6px 44px; }
    .gutter { left:2px; top:6px; width:36px; font-size:12px; padding:6px 4px; }
    .md li .li-gutter, .md tr .row-gutter {
      display:inline; left:auto; right:auto; position:static;
      margin-left:6px; padding:2px 8px; font-size:11px; background:#2d3a4e; color:#888;
    }
    .md li .li-gutter.has-comment, .md tr .row-gutter.has-comment { color:#f48771; }
    .comment-block, .composer { margin-left: 8px; margin-right: 0; }
    .composer textarea { min-height: 90px; font-size: 15px; }
    .composer button { padding: 10px 16px; font-size: 14px; }
    .md table { display:block; overflow-x:auto; max-width: 100%; }
    .md pre { font-size: 11px; }
    .ln { width: 38px; font-size: 12px; padding: 6px 8px 6px 4px; }
    .lc { font-size: 12px; }
  }
</style></head>
<body>
<div id="sidebar">
  <h2>Architect Output</h2>
  <div class="branch">branch: {branch}</div>
  <ul id="filelist"></ul>
</div>
<div id="main">
  <div id="header">
    <button id="hamburger" aria-label="Files">☰</button>
    <h1 id="filename">Select a file</h1>
    <button id="send-architect" title="Mark comments ready and copy a prompt to clipboard">📨 Send to Architect</button>
    <span class="toggle" id="toggle-view">view: rendered ▾</span>
  </div>
  <div id="toast"></div>
  <div id="scrim"></div>
  <div id="content" class="md"><div class="empty">Pick a file from the left.</div></div>
</div>

<script>
let currentFile = null;
let comments = {};
let files = [];
let viewMode = 'rendered';   // or 'raw'
let currentRaw = '';         // raw text of current file
let currentBlocks = [];      // server-parsed blocks for current file
let modifiedSinceReview = []; // files touched after last "Send to Architect"

async function loadFiles() {
  const r = await fetch('/api/files');
  const j = await r.json();
  files = j.files;
  comments = j.comments;
  modifiedSinceReview = j.modifiedSinceReview || [];
  renderSidebar();
  if (files.length && !currentFile) {
    let initial = files[0];
    if (location.hash.length > 1) {
      const fromHash = decodeURIComponent(location.hash.slice(1));
      if (files.includes(fromHash)) initial = fromHash;
    }
    // Replace (not push) so the back button doesn't first land on a blank entry.
    await openFile(initial, { push: false });
    history.replaceState({ path: initial, scroll: 0 }, '', '#' + encodeURIComponent(initial));
  }
}

function renderSidebar() {
  const ul = document.getElementById('filelist');
  ul.innerHTML = '';
  const modSet = new Set(modifiedSinceReview);
  for (const f of files) {
    const li = document.createElement('li');
    if (modSet.has(f)) {
      const dot = document.createElement('span');
      dot.className = 'modified';
      dot.title = 'Modified since last review request';
      li.appendChild(dot);
    }
    li.appendChild(document.createTextNode(f));
    if (f === currentFile) li.className = 'active';
    const count = (comments[f] || []).length;
    if (count) {
      const b = document.createElement('span');
      b.className = 'badge'; b.textContent = count;
      li.appendChild(b);
    }
    li.onclick = () => openFile(f);
    ul.appendChild(li);
  }
}

async function openFile(path, opts) {
  opts = opts || {};
  const main = document.getElementById('main');
  const isRefresh = (currentFile === path);
  // Save scroll of the file we're leaving into the current history entry,
  // so going forward/back returns to the exact location.
  if (currentFile && !isRefresh) {
    history.replaceState({ path: currentFile, scroll: main ? main.scrollTop : 0 }, '');
  }
  // Default scroll: preserve on refresh (e.g., after saving a comment), top on navigate.
  let scroll;
  if (opts.scroll != null) scroll = opts.scroll;
  else if (isRefresh) scroll = main ? main.scrollTop : 0;
  else scroll = 0;
  currentFile = path;
  document.getElementById('filename').textContent = path;
  const r = await fetch('/api/file?path=' + encodeURIComponent(path));
  const j = await r.json();
  currentRaw = j.text;
  currentBlocks = j.blocks;
  renderView();
  renderSidebar();
  main.scrollTop = scroll;
  if (opts.push !== false && !isRefresh) {
    history.pushState({ path: path, scroll: 0 }, '', '#' + encodeURIComponent(path));
  }
}

window.addEventListener('popstate', (ev) => {
  const st = ev.state;
  if (st && st.path) {
    openFile(st.path, { push: false, scroll: st.scroll || 0 });
  }
});

function renderView() {
  const c = document.getElementById('content');
  c.innerHTML = '';
  c.className = viewMode === 'raw' ? 'raw' : 'md';
  document.getElementById('toggle-view').textContent =
    'view: ' + viewMode + ' ▾';
  if (viewMode === 'raw') renderRaw(); else renderRendered();
}

function fileComments() {
  return (comments[currentFile] || []).slice().sort((a,b)=>a.line-b.line);
}

function renderRendered() {
  const c = document.getElementById('content');
  const fc = fileComments();
  // Index comments by line
  const byLine = {};
  for (const cm of fc) (byLine[cm.line] ||= []).push(cm);

  for (const blk of currentBlocks) {
    const wrap = document.createElement('div');
    wrap.className = 'block';
    wrap.dataset.start = blk.start;
    wrap.dataset.end = blk.end;
    const gut = document.createElement('span');
    gut.className = 'gutter';
    gut.textContent = blk.start;
    gut.title = 'Comment on line ' + blk.start;
    gut.onclick = (e) => { e.stopPropagation(); showComposer(blk.start, wrap); };
    wrap.appendChild(gut);
    const body = document.createElement('div');
    body.innerHTML = blk.html;
    wrap.appendChild(body);
    c.appendChild(wrap);

    // Per-LI and per-TR gutters
    body.querySelectorAll('li[data-line], tr[data-line]').forEach(el => {
      const lineNo = parseInt(el.dataset.line, 10);
      const g = document.createElement('span');
      g.className = el.tagName === 'LI' ? 'li-gutter' : 'row-gutter';
      g.textContent = lineNo;
      g.title = 'Comment on line ' + lineNo;
      g.onclick = (e) => { e.stopPropagation(); showComposer(lineNo, el); };
      // For TR we need to attach to first cell so it positions correctly
      if (el.tagName === 'TR') {
        const firstCell = el.querySelector('th, td');
        if (firstCell) { firstCell.style.position = 'relative'; firstCell.appendChild(g); }
        else el.appendChild(g);
      } else {
        el.appendChild(g);
      }
    });

    // Mark gutter if comments exist anywhere in block range
    const subAnchored = new Set();
    body.querySelectorAll('li[data-line], tr[data-line]').forEach(el => {
      subAnchored.add(parseInt(el.dataset.line, 10));
    });
    const hasOpenInRange = fc.some(cm =>
      cm.line >= blk.start && cm.line <= blk.end &&
      (cm.status || 'open') === 'open' && !subAnchored.has(cm.line)
    );
    if (hasOpenInRange) gut.classList.add('has-comment');

    // Mark per-LI / per-TR gutters (only for OPEN comments on that line)
    body.querySelectorAll('li[data-line], tr[data-line]').forEach(el => {
      const lineNo = parseInt(el.dataset.line, 10);
      const open = (byLine[lineNo] || []).some(cm => (cm.status || 'open') === 'open');
      if (open) {
        const cls = el.tagName === 'LI' ? '.li-gutter' : '.row-gutter';
        const g = el.querySelector(cls);
        if (g) g.classList.add('has-comment');
      }
    });

    // Render comments for this block: those without a sub-anchor go after the block
    for (const cm of fc) {
      if (cm.line < blk.start || cm.line > blk.end) continue;
      if (subAnchored.has(cm.line)) continue;
      c.appendChild(makeCommentBlock(cm));
    }

    // For sub-anchored lines (li/tr with comments), append comment blocks after the parent block
    body.querySelectorAll('li[data-line], tr[data-line]').forEach(el => {
      const lineNo = parseInt(el.dataset.line, 10);
      for (const cm of (byLine[lineNo] || [])) c.appendChild(makeCommentBlock(cm));
    });
  }
}

function renderRaw() {
  const c = document.getElementById('content');
  const lines = currentRaw.split('\n');
  const fc = fileComments();
  const byLine = {};
  for (const cm of fc) (byLine[cm.line] ||= []).push(cm);
  lines.forEach((text, i) => {
    const lineNo = i + 1;
    const row = document.createElement('div');
    row.className = 'line-row';
    const ln = document.createElement('div');
    ln.className = 'ln';
    if (byLine[lineNo]) ln.classList.add('has-comment');
    ln.textContent = lineNo;
    ln.onclick = () => showComposer(lineNo, row);
    const lc = document.createElement('div');
    lc.className = 'lc'; lc.textContent = text || ' ';
    row.appendChild(ln); row.appendChild(lc);
    c.appendChild(row);
    if (byLine[lineNo]) {
      for (const cm of byLine[lineNo]) c.appendChild(makeCommentBlock(cm));
    }
  });
}

function makeCommentBlock(cm) {
  const cb = document.createElement('div');
  const author = cm.author || 'user';
  const status = cm.status || 'open';
  cb.className = 'comment-block author-' + author + ' status-' + status;
  if (cm.parent_id) cb.classList.add('is-reply');

  const meta = document.createElement('div');
  meta.className = 'meta';
  const who = document.createElement('span');
  who.className = 'who';
  who.textContent = author === 'architect' ? '🏛 architect' : '👤 you';
  meta.appendChild(who);
  const lineInfo = document.createElement('span');
  lineInfo.textContent = '@ line ' + cm.line + ' · ' + cm.ts;
  meta.appendChild(lineInfo);
  if (status !== 'open') {
    const b = document.createElement('span');
    b.className = 'badge ' + status;
    b.textContent = status;
    meta.appendChild(b);
  }
  cb.appendChild(meta);

  const body = document.createElement('div');
  body.className = 'body'; body.textContent = cm.text;
  cb.appendChild(body);

  const actions = document.createElement('div');
  actions.className = 'actions';
  const reply = document.createElement('button');
  reply.className = 'reply'; reply.textContent = '↳ reply';
  reply.onclick = () => showComposer(cm.line, cb, { parent_id: cm.id, author: 'user' });
  actions.appendChild(reply);
  if (status === 'open') {
    const res = document.createElement('button');
    res.className = 'resolve'; res.textContent = 'Mark resolved';
    res.title = 'Mark this comment as resolved';
    res.onclick = () => updateStatus(cm.id, 'resolved');
    actions.appendChild(res);
  } else {
    const reopen = document.createElement('button');
    reopen.textContent = 'Reopen';
    reopen.title = 'Reopen this comment';
    reopen.onclick = () => updateStatus(cm.id, 'open');
    actions.appendChild(reopen);
  }
  const del = document.createElement('button');
  del.className = 'del'; del.textContent = '✕';
  del.title = 'Delete';
  del.onclick = () => deleteComment(cm.id);
  actions.appendChild(del);
  cb.appendChild(actions);

  return cb;
}

async function updateStatus(id, status) {
  const r = await fetch('/api/comment?id=' + encodeURIComponent(id), {
    method: 'PATCH', headers: {'Content-Type':'application/json'},
    body: JSON.stringify({ status })
  });
  const j = await r.json();
  comments = j.comments;
  await openFile(currentFile);
}

function showComposer(lineNo, anchorEl, opts) {
  opts = opts || {};
  document.querySelectorAll('.composer').forEach(e => e.remove());
  const comp = document.createElement('div');
  comp.className = 'composer';
  const label = opts.parent_id ? `Reply to comment @ line ${lineNo}` : `Comment on line ${lineNo}`;
  comp.innerHTML = `
    <textarea placeholder="${label}..."></textarea>
    <div class="row">
      <span class="target">${currentFile} : line ${lineNo}${opts.parent_id ? ' (reply)' : ''}</span>
      <button class="save">Save</button>
      <button class="cancel">Cancel</button>
    </div>`;
  anchorEl.insertAdjacentElement('afterend', comp);
  comp.querySelector('.cancel').onclick = () => comp.remove();
  comp.querySelector('.save').onclick = () => submitComment(lineNo, comp, opts);
  comp.querySelector('textarea').focus();
}

async function submitComment(lineNo, comp, opts) {
  opts = opts || {};
  const text = comp.querySelector('textarea').value.trim();
  if (!text) return;
  const r = await fetch('/api/comment', {
    method:'POST', headers:{'Content-Type':'application/json'},
    body: JSON.stringify({
      file: currentFile, line: lineNo, text,
      parent_id: opts.parent_id || null,
      author: opts.author || 'user'
    })
  });
  const j = await r.json();
  comments = j.comments;
  await openFile(currentFile);
}

async function deleteComment(id) {
  if (!confirm('Delete this comment?')) return;
  const r = await fetch('/api/comment?id=' + encodeURIComponent(id), { method:'DELETE' });
  const j = await r.json();
  comments = j.comments;
  await openFile(currentFile);
}

document.getElementById('toggle-view').onclick = () => {
  viewMode = viewMode === 'rendered' ? 'raw' : 'rendered';
  if (currentFile) renderView();
};

function resolveRelPath(base, rel) {
  if (rel.startsWith('/')) return rel.replace(/^\/+/, '');
  const stack = base ? base.split('/') : [];
  for (const seg of rel.split('/')) {
    if (seg === '..') stack.pop();
    else if (seg && seg !== '.') stack.push(seg);
  }
  return stack.join('/');
}

document.getElementById('content').addEventListener('click', (ev) => {
  const a = ev.target.closest('a');
  if (!a) return;
  const href = a.getAttribute('href');
  if (!href || /^(https?:|mailto:|#)/i.test(href)) return;
  const dir = currentFile && currentFile.includes('/')
    ? currentFile.slice(0, currentFile.lastIndexOf('/'))
    : '';
  const [pathPart] = href.split('#');
  const resolved = resolveRelPath(dir, pathPart);
  if (files.includes(resolved)) {
    ev.preventDefault();
    openFile(resolved);
  }
});

// Send to Architect button
document.getElementById('send-architect').onclick = async () => {
  const r = await fetch('/api/request-review', { method: 'POST' });
  const j = await r.json();
  const prompt = `read my comments at ${j.commentsPath} and address them`;
  try { await navigator.clipboard.writeText(prompt); } catch (e) {}
  // Reset the "modified since review" indicators — review-requested.json was just touched.
  modifiedSinceReview = [];
  renderSidebar();
  showToast(j.count, prompt);
};

// Auto-poll for files modified since last review (architect may be working in background).
async function refreshModifiedDots() {
  try {
    const r = await fetch('/api/files');
    const j = await r.json();
    const next = j.modifiedSinceReview || [];
    const a = next.slice().sort().join('|');
    const b = modifiedSinceReview.slice().sort().join('|');
    if (a !== b) {
      modifiedSinceReview = next;
      renderSidebar();
    }
  } catch (e) {}
}
setInterval(refreshModifiedDots, 5000);

function showToast(count, prompt) {
  const t = document.getElementById('toast');
  t.innerHTML = `<span class="close" onclick="document.getElementById('toast').classList.remove('show')">✕</span>
    <strong>${count} comment${count===1?'':'s'} marked for review.</strong><br>
    Prompt copied to clipboard — paste into Claude:
    <div class="prompt">${prompt}</div>`;
  t.classList.add('show');
  setTimeout(() => t.classList.remove('show'), 12000);
}

// Mobile drawer
const drawerToggle = () => document.body.classList.toggle('drawer-open');
document.getElementById('hamburger').onclick = drawerToggle;
document.getElementById('scrim').onclick = drawerToggle;
// Auto-close drawer when picking a file on mobile
const _origOpen = openFile;
openFile = async (p, opts) => { await _origOpen(p, opts); if (window.innerWidth <= 760) document.body.classList.remove('drawer-open'); };

loadFiles();
</script>
</body></html>
"""


class Handler(http.server.BaseHTTPRequestHandler):
    def log_message(self, fmt, *args):
        pass

    def _send(self, code, body, ctype="application/json"):
        data = body if isinstance(body, bytes) else body.encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", ctype + "; charset=utf-8")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def _json(self, code, obj):
        self._send(code, json.dumps(obj, ensure_ascii=False), "application/json")

    def do_GET(self):
        u = urllib.parse.urlparse(self.path)
        if u.path in ("/", "/index.html"):
            html = INDEX_HTML.replace("{branch}", current_branch())
            self._send(200, html, "text/html")
            return
        if u.path == "/api/files":
            self._json(200, {
                "files": list_files(),
                "comments": load_comments_normalized(),
                "modifiedSinceReview": files_modified_since_review(),
            })
            return
        if u.path == "/api/file":
            qs = urllib.parse.parse_qs(u.query)
            rel = qs.get("path", [""])[0]
            base = architect_dir().resolve()
            target = (base / rel).resolve()
            if not str(target).startswith(str(base)) or not target.exists():
                self._json(404, {"error": "not found"})
                return
            text = target.read_text(encoding="utf-8", errors="replace")
            blocks = render_markdown(text)
            self._json(200, {"text": text, "blocks": blocks})
            return
        self._send(404, "not found", "text/plain")

    def do_DELETE(self):
        u = urllib.parse.urlparse(self.path)
        if u.path != "/api/comment":
            self._send(404, "not found", "text/plain"); return
        cid = urllib.parse.parse_qs(u.query).get("id", [""])[0]
        comments = load_comments_normalized()
        for f, arr in list(comments.items()):
            comments[f] = [c for c in arr if c.get("id") != cid]
            if not comments[f]:
                del comments[f]
        save_comments(comments)
        self._json(200, {"comments": comments})

    def do_PATCH(self):
        u = urllib.parse.urlparse(self.path)
        if u.path != "/api/comment":
            self._send(404, "not found", "text/plain"); return
        cid = urllib.parse.parse_qs(u.query).get("id", [""])[0]
        length = int(self.headers.get("Content-Length", "0"))
        body = json.loads(self.rfile.read(length).decode("utf-8")) if length else {}
        new_status = body.get("status")
        if new_status not in ("open", "resolved", "disagreed"):
            self._json(400, {"error": "invalid status"}); return
        comments = load_comments_normalized()
        for f, arr in comments.items():
            for c in arr:
                if c.get("id") == cid:
                    c["status"] = new_status
        save_comments(comments)
        self._json(200, {"comments": comments})

    def do_POST(self):
        u = urllib.parse.urlparse(self.path)
        if u.path == "/api/request-review":
            import datetime
            comments = load_comments_normalized()
            count = sum(len(v) for v in comments.values())
            rel = comments_path().relative_to(REPO_ROOT)
            marker = architect_dir() / "review-requested.json"
            marker.parent.mkdir(parents=True, exist_ok=True)
            marker.write_text(json.dumps({
                "ts": datetime.datetime.now().isoformat(timespec="seconds"),
                "count": count,
                "comments_path": str(rel),
            }, indent=2))
            print(f"[review requested] {count} comment(s) — {rel}")
            self._json(200, {"count": count, "commentsPath": str(rel)})
            return
        if u.path != "/api/comment":
            self._send(404, "not found", "text/plain"); return
        length = int(self.headers.get("Content-Length", "0"))
        body = json.loads(self.rfile.read(length).decode("utf-8"))
        f = body["file"]; line = int(body["line"]); text = body["text"]
        author = body.get("author", "user")
        parent_id = body.get("parent_id")
        import uuid, datetime
        entry = {
            "id": uuid.uuid4().hex[:10],
            "line": line,
            "text": text,
            "author": author,
            "status": "open",
            "parent_id": parent_id,
            "ts": datetime.datetime.now().isoformat(timespec="seconds"),
        }
        comments = load_comments_normalized()
        comments.setdefault(f, []).append(entry)
        comments[f].sort(key=lambda c: (c["line"], c["ts"]))
        save_comments(comments)
        self._json(200, {"comments": comments})


def main():
    branch = current_branch()
    base = architect_dir()
    print(f"Architect review server")
    print(f"  branch:   {branch}")
    print(f"  serving:  {base}")
    print(f"  comments: {comments_path()}")
    print(f"  url:      http://localhost:{PORT}")
    if not base.exists():
        print(f"  WARNING: {base} does not exist — no files to show.")
    http.server.ThreadingHTTPServer(("0.0.0.0", PORT), Handler).serve_forever()


if __name__ == "__main__":
    main()
