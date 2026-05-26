#!/usr/bin/env python3
"""Lightweight code review web UI for the PLang C# code.

Run:   python3 .webui/server.py
Open:  http://localhost:8080
"""
import json
import os
import re
import subprocess
import sys
import time
import urllib.parse
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
COMMENTS_FILE = os.path.join(os.path.dirname(__file__), "comments.json")
PORT = 8080

# Folders to skip when walking
SKIP_DIRS = {"bin", "obj", "node_modules", ".git", ".vs", "TestResults",
             "packages", "publish", "Publish", ".webui"}
SKIP_BUILD = re.compile(r"(^|/)\.build(/|$)")


def git(*args, cwd=ROOT):
    try:
        r = subprocess.run(["git", *args], cwd=cwd, capture_output=True, text=True, timeout=30)
        return r.stdout
    except Exception as e:
        return ""


def load_comments():
    if not os.path.exists(COMMENTS_FILE):
        return {}
    try:
        with open(COMMENTS_FILE, "r", encoding="utf-8") as f:
            return json.load(f)
    except Exception:
        return {}


def save_comments(c):
    with open(COMMENTS_FILE, "w", encoding="utf-8") as f:
        json.dump(c, f, indent=2)


def safe_path(rel):
    """Resolve a relative path safely under ROOT."""
    rel = rel.lstrip("/").replace("\\", "/")
    full = os.path.abspath(os.path.join(ROOT, rel))
    if not full.startswith(ROOT + os.sep) and full != ROOT:
        return None
    return full


def build_tree():
    """Walk ROOT, return nested dict of folders → files containing only .cs files
    (folders without .cs descendants are pruned)."""
    tree = {"name": "", "path": "", "type": "dir", "children": []}

    # Build a flat list of .cs files, then nest.
    cs_files = []
    for dirpath, dirnames, filenames in os.walk(ROOT):
        # Filter dirs in-place
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS and not d.startswith(".")]
        rel_dir = os.path.relpath(dirpath, ROOT).replace("\\", "/")
        if rel_dir == ".":
            rel_dir = ""
        if SKIP_BUILD.search("/" + rel_dir + "/"):
            continue
        for fn in filenames:
            if not fn.endswith(".cs"):
                continue
            rel = (rel_dir + "/" + fn) if rel_dir else fn
            cs_files.append(rel)

    cs_files.sort()
    for rel in cs_files:
        parts = rel.split("/")
        node = tree
        cur_path = ""
        for i, p in enumerate(parts):
            cur_path = (cur_path + "/" + p) if cur_path else p
            is_last = i == len(parts) - 1
            existing = next((c for c in node["children"] if c["name"] == p), None)
            if existing is None:
                existing = {
                    "name": p,
                    "path": cur_path,
                    "type": "file" if is_last else "dir",
                    "children": [] if not is_last else None,
                }
                node["children"].append(existing)
            node = existing
    return tree


def changed_files():
    """Files changed on this branch vs origin/main, ordered by most-recent commit."""
    base_candidates = ["origin/main", "main", "origin/master", "master"]
    base = None
    for b in base_candidates:
        if git("rev-parse", "--verify", b).strip():
            base = b
            break
    if not base:
        return []

    merge_base = git("merge-base", "HEAD", base).strip() or base

    # All committed changes on this branch
    out = git("diff", "--name-only", f"{merge_base}...HEAD")
    committed = [f for f in out.splitlines() if f.endswith(".cs")]

    # Working-tree + staged changes ("this session" if not yet committed)
    out2 = git("status", "--porcelain")
    wt = []
    for line in out2.splitlines():
        if len(line) < 4:
            continue
        fn = line[3:].strip()
        # rename "old -> new"
        if " -> " in fn:
            fn = fn.split(" -> ", 1)[1]
        if fn.endswith(".cs"):
            wt.append(fn)

    # Last-touched commit time per committed file
    files = []
    seen = set()
    for f in committed:
        if f in seen:
            continue
        seen.add(f)
        ts = git("log", "-1", "--format=%ct", f"{merge_base}..HEAD", "--", f).strip()
        msg = git("log", "-1", "--format=%s", f"{merge_base}..HEAD", "--", f).strip()
        sha = git("log", "-1", "--format=%h", f"{merge_base}..HEAD", "--", f).strip()
        try:
            ts_i = int(ts)
        except Exception:
            ts_i = 0
        files.append({"path": f, "ts": ts_i, "msg": msg, "sha": sha,
                      "uncommitted": f in wt})

    # Add uncommitted-only files (not in committed list)
    for f in wt:
        if f in seen:
            continue
        seen.add(f)
        files.append({"path": f, "ts": int(time.time()), "msg": "(uncommitted)",
                      "sha": "", "uncommitted": True})

    files.sort(key=lambda x: x["ts"], reverse=True)
    return files


def session_changed_lines(rel):
    """Return set of 1-based line numbers in the *current* file that differ from HEAD
    (i.e., changes in this working session, staged or unstaged)."""
    tracked = subprocess.run(["git", "cat-file", "-e", f"HEAD:{rel}"],
                             cwd=ROOT, capture_output=True).returncode == 0
    if tracked:
        out = git("diff", "HEAD", "--unified=0", "--", rel)
    else:
        out = git("diff", "--no-index", "--unified=0", "/dev/null", rel)
    lines = set()
    for line in out.splitlines():
        m = re.match(r"^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@", line)
        if m:
            start = int(m.group(1))
            count = int(m.group(2) or "1")
            for i in range(start, start + max(count, 1)):
                lines.add(i)
    return sorted(lines)


def branch_changed_lines(rel):
    """Lines in current file changed on this branch (vs merge-base)."""
    base_candidates = ["origin/main", "main", "origin/master", "master"]
    base = None
    for b in base_candidates:
        if git("rev-parse", "--verify", b).strip():
            base = b
            break
    if not base:
        return []
    mb = git("merge-base", "HEAD", base).strip() or base
    out = git("diff", mb, "--unified=0", "--", rel)
    lines = set()
    for line in out.splitlines():
        m = re.match(r"^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@", line)
        if m:
            start = int(m.group(1))
            count = int(m.group(2) or "1")
            for i in range(start, start + max(count, 1)):
                lines.add(i)
    return sorted(lines)


# ---------------- HTTP ----------------

INDEX_HTML = r"""<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>PLang C# Review</title>
<link rel="stylesheet" href="https://cdn.jsdelivr.net/gh/highlightjs/cdn-release@11.9.0/build/styles/github-dark.min.css">
<style>
:root { --bg:#1e1e1e; --panel:#252526; --panel2:#2d2d30; --fg:#d4d4d4; --muted:#9aa0a6;
        --accent:#0e639c; --accent2:#1177bb; --border:#3c3c3c; --change:#f6c84c; --session:#3fb950; }
* { box-sizing: border-box; }
html, body { margin:0; height:100%; background:var(--bg); color:var(--fg);
             font-family: -apple-system, "Segoe UI", Roboto, sans-serif; font-size:13px; }
header { height:42px; background:#323233; border-bottom:1px solid var(--border);
         display:flex; align-items:center; padding:0 14px; gap:14px; }
header h1 { font-size:14px; margin:0; font-weight:600; }
header .path { color:var(--muted); font-family: ui-monospace, Menlo, monospace; font-size:12px; }
.layout { display:grid; grid-template-columns: 320px 1fr; height: calc(100vh - 42px); }
aside { background:var(--panel); border-right:1px solid var(--border); overflow:hidden;
        display:flex; flex-direction:column; }
.section-title { padding:8px 12px; font-size:11px; text-transform:uppercase; letter-spacing:.5px;
                 color:var(--muted); background:var(--panel2); border-bottom:1px solid var(--border); }
#changed { max-height:35%; overflow:auto; border-bottom:1px solid var(--border); }
#changed .item { padding:6px 12px; cursor:pointer; border-bottom:1px solid #2a2a2a; }
#changed .item:hover { background:#2a2d2e; }
#changed .item.active { background:#094771; }
#changed .name { font-family:ui-monospace,Menlo,monospace; font-size:12px; overflow:hidden;
                 text-overflow:ellipsis; white-space:nowrap; }
#changed .meta { color:var(--muted); font-size:11px; margin-top:2px; display:flex; gap:6px; }
#changed .uncommitted { color:var(--session); }
#tree { flex:1; overflow:auto; padding:6px 0; }
.node { padding:2px 0; user-select:none; }
.node .row { padding:2px 8px; cursor:pointer; white-space:nowrap; display:flex; align-items:center; gap:4px; }
.node .row:hover { background:#2a2d2e; }
.node .row.active { background:#094771; }
.node .caret { width:12px; display:inline-block; color:var(--muted); }
.node .name { font-family:ui-monospace,Menlo,monospace; font-size:12px; }
.node.dir > .row .name { color:#cfcfcf; }
.node.file > .row .name { color:#9cdcfe; }
.children { padding-left:14px; display:none; }
.node.open > .children { display:block; }
main { overflow:auto; background:var(--bg); position:relative; }
#empty { padding:30px; color:var(--muted); }
#viewer { display:none; }
#filebar { position:sticky; top:0; background:#2d2d30; padding:6px 12px;
           border-bottom:1px solid var(--border); display:flex; gap:12px; align-items:center;
           font-family:ui-monospace,Menlo,monospace; font-size:12px; z-index:5; }
#filebar .legend { color:var(--muted); margin-left:auto; display:flex; gap:12px; }
#filebar .swatch { display:inline-block; width:10px; height:10px; vertical-align:middle;
                   margin-right:4px; border-radius:2px; }
.code-table { width:100%; border-collapse:collapse; font-family:ui-monospace,Menlo,monospace;
              font-size:12.5px; line-height:1.5; }
.code-table td { vertical-align:top; padding:0; }
.code-table td.gutter { width:50px; text-align:right; padding:0 8px; color:#6e7681;
                        user-select:none; border-right:1px solid #2a2a2a; cursor:pointer;
                        background:var(--panel); }
.code-table td.gutter:hover { background:#37373d; color:#fff; }
.code-table td.gutter.session { border-right:3px solid var(--session); }
.code-table td.gutter.branch { background:#3a2f0b; }
.code-table td.code { padding:0 10px; white-space:pre; }
.code-table tr.has-comments td.gutter::after { content:" 💬"; }
.comments-row td { padding:6px 10px 6px 60px; background:#0f1922; border-top:1px solid #1c2a34;
                   border-bottom:1px solid #1c2a34; }
.comment { background:#1c2a34; border:1px solid #30363d; border-radius:4px; padding:6px 10px;
           margin-bottom:6px; }
.comment .meta { color:var(--muted); font-size:11px; margin-bottom:3px; }
.comment .body { white-space:pre-wrap; }
.comment-form textarea { width:100%; background:#0d1117; color:var(--fg);
                         border:1px solid #30363d; border-radius:4px; padding:6px;
                         font-family:inherit; font-size:12.5px; min-height:50px; resize:vertical; }
.comment-form .actions { margin-top:4px; display:flex; gap:6px; }
button { background:var(--accent); color:#fff; border:0; padding:5px 12px; border-radius:3px;
         cursor:pointer; font-size:12px; }
button:hover { background:var(--accent2); }
button.ghost { background:transparent; color:var(--muted); }
button.ghost:hover { background:#2a2d2e; color:#fff; }
</style>
</head>
<body>
<header>
  <h1>PLang C# Review</h1>
  <span class="path" id="currentPath"></span>
</header>
<div class="layout">
  <aside>
    <div class="section-title">Recent changes (branch)</div>
    <div id="changed"></div>
    <div class="section-title">Files</div>
    <div id="tree"></div>
  </aside>
  <main>
    <div id="empty">Select a .cs file from the tree or recent changes panel.</div>
    <div id="viewer">
      <div id="filebar">
        <span id="fbpath"></span>
        <span class="legend">
          <span><span class="swatch" style="background:var(--session)"></span>this session</span>
          <span><span class="swatch" style="background:#3a2f0b"></span>this branch</span>
        </span>
      </div>
      <table class="code-table" id="codeTable"><tbody></tbody></table>
    </div>
  </main>
</div>

<script src="https://cdn.jsdelivr.net/gh/highlightjs/cdn-release@11.9.0/build/highlight.min.js"></script>
<script src="https://cdn.jsdelivr.net/gh/highlightjs/cdn-release@11.9.0/build/languages/csharp.min.js"></script>
<script>
let CURRENT = null;
let COMMENTS = {};

async function api(url, opts) {
  const r = await fetch(url, opts);
  return await r.json();
}

function renderTree(node, container, depth=0) {
  if (!node.children) return;
  for (const c of node.children) {
    const div = document.createElement('div');
    div.className = 'node ' + c.type;
    const row = document.createElement('div');
    row.className = 'row';
    row.style.paddingLeft = (8 + depth*12) + 'px';
    const caret = document.createElement('span');
    caret.className = 'caret';
    caret.textContent = c.type === 'dir' ? '▸' : '';
    const name = document.createElement('span');
    name.className = 'name';
    name.textContent = c.name;
    row.appendChild(caret);
    row.appendChild(name);
    div.appendChild(row);
    if (c.type === 'dir') {
      const ch = document.createElement('div');
      ch.className = 'children';
      div.appendChild(ch);
      renderTree(c, ch, depth+1);
      row.addEventListener('click', () => {
        div.classList.toggle('open');
        caret.textContent = div.classList.contains('open') ? '▾' : '▸';
      });
    } else {
      row.dataset.path = c.path;
      row.addEventListener('click', () => openFile(c.path));
    }
    container.appendChild(div);
  }
}

function fmtTime(ts) {
  if (!ts) return '';
  const d = new Date(ts*1000);
  const now = Date.now()/1000;
  const diff = now - ts;
  if (diff < 60) return 'just now';
  if (diff < 3600) return Math.floor(diff/60)+'m ago';
  if (diff < 86400) return Math.floor(diff/3600)+'h ago';
  return Math.floor(diff/86400)+'d ago';
}

async function loadChanged() {
  const items = await api('/api/changed');
  const c = document.getElementById('changed');
  c.innerHTML = '';
  if (!items.length) { c.innerHTML = '<div style="padding:10px 12px;color:var(--muted);font-size:12px;">No changes vs main.</div>'; return; }
  for (const it of items) {
    const div = document.createElement('div');
    div.className = 'item';
    div.dataset.path = it.path;
    div.innerHTML = '<div class="name">'+escapeHtml(it.path.split('/').pop())+'</div>'+
                    '<div class="meta">'+
                       (it.uncommitted?'<span class="uncommitted">●</span>':'')+
                       '<span>'+fmtTime(it.ts)+'</span>'+
                       (it.sha?'<span>'+it.sha+'</span>':'')+
                    '</div>'+
                    '<div class="meta" style="color:#777;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;">'+escapeHtml(it.path)+'</div>';
    div.addEventListener('click', () => openFile(it.path));
    c.appendChild(div);
  }
}

function escapeHtml(s) {
  return s.replace(/[&<>"']/g, ch => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[ch]));
}

async function openFile(path) {
  CURRENT = path;
  document.getElementById('currentPath').textContent = path;
  document.getElementById('fbpath').textContent = path;
  document.querySelectorAll('#changed .item').forEach(el => el.classList.toggle('active', el.dataset.path === path));
  document.querySelectorAll('#tree .row').forEach(el => el.classList.toggle('active', el.dataset.path === path));

  const data = await api('/api/file?path=' + encodeURIComponent(path));
  COMMENTS = data.comments || {};
  renderCode(data);
  document.getElementById('empty').style.display = 'none';
  document.getElementById('viewer').style.display = 'block';
}

function renderCode(data) {
  const tbody = document.querySelector('#codeTable tbody');
  tbody.innerHTML = '';
  const sessionSet = new Set(data.session_lines || []);
  const branchSet = new Set(data.branch_lines || []);
  // Highlight the whole file once, then split into lines preserving spans.
  const result = hljs.highlight(data.content, {language: 'csharp', ignoreIllegals: true});
  const htmlLines = splitHighlightedLines(result.value);
  for (let i = 0; i < htmlLines.length; i++) {
    const lineNo = i + 1;
    const tr = document.createElement('tr');
    tr.dataset.line = lineNo;
    let gClass = 'gutter';
    if (sessionSet.has(lineNo)) gClass += ' session';
    if (branchSet.has(lineNo)) gClass += ' branch';
    const hasC = (COMMENTS[String(lineNo)] || []).length > 0;
    if (hasC) tr.classList.add('has-comments');
    tr.innerHTML = '<td class="'+gClass+'">'+lineNo+'</td>'+
                   '<td class="code">'+ (htmlLines[i] || '&nbsp;') +'</td>';
    tr.querySelector('td.gutter').addEventListener('click', () => toggleCommentRow(lineNo, tr));
    tbody.appendChild(tr);
    if (hasC) appendCommentsRow(tr, lineNo, /*withForm=*/false);
  }
}

function splitHighlightedLines(html) {
  // Split highlighted HTML by \n while keeping <span> tags balanced per line.
  const lines = [];
  const stack = [];
  let cur = '';
  let i = 0;
  const flush = () => {
    let line = cur;
    // close open tags for this line, then re-open on next
    for (let k = stack.length-1; k>=0; k--) line += '</span>';
    lines.push(line);
    cur = '';
    for (const t of stack) cur += t;
  };
  while (i < html.length) {
    const ch = html[i];
    if (ch === '<') {
      const end = html.indexOf('>', i);
      if (end === -1) { cur += html.slice(i); break; }
      const tag = html.slice(i, end+1);
      if (tag.startsWith('</')) {
        cur += tag; stack.pop();
      } else if (tag.startsWith('<span')) {
        cur += tag; stack.push(tag);
      } else {
        cur += tag;
      }
      i = end+1;
    } else if (ch === '\n') {
      flush(); i++;
    } else {
      cur += ch; i++;
    }
  }
  if (cur.length || lines.length === 0) flush();
  return lines;
}

function appendCommentsRow(afterTr, lineNo, withForm) {
  // Remove existing row first
  const next = afterTr.nextSibling;
  if (next && next.classList && next.classList.contains('comments-row')) next.remove();
  const tr = document.createElement('tr');
  tr.className = 'comments-row';
  const td = document.createElement('td'); td.colSpan = 2;
  const list = COMMENTS[String(lineNo)] || [];
  for (const c of list) {
    const d = document.createElement('div'); d.className='comment';
    d.innerHTML = '<div class="meta">'+escapeHtml(new Date(c.ts*1000).toLocaleString())+'</div>'+
                  '<div class="body">'+escapeHtml(c.text)+'</div>';
    td.appendChild(d);
  }
  if (withForm) {
    const form = document.createElement('div'); form.className='comment-form';
    const ta = document.createElement('textarea'); ta.placeholder='Comment on line '+lineNo+'…';
    form.appendChild(ta);
    const actions = document.createElement('div'); actions.className='actions';
    const save = document.createElement('button'); save.textContent='Add comment';
    const cancel = document.createElement('button'); cancel.className='ghost'; cancel.textContent='Cancel';
    actions.appendChild(save); actions.appendChild(cancel);
    form.appendChild(actions);
    td.appendChild(form);
    ta.focus();
    save.addEventListener('click', async () => {
      const text = ta.value.trim();
      if (!text) return;
      const r = await api('/api/comments', {
        method:'POST', headers:{'Content-Type':'application/json'},
        body: JSON.stringify({path: CURRENT, line: lineNo, text})
      });
      COMMENTS = r.comments;
      // Re-render to refresh gutter dot
      const codeRow = afterTr;
      codeRow.classList.add('has-comments');
      appendCommentsRow(codeRow, lineNo, false);
    });
    cancel.addEventListener('click', () => tr.remove());
  }
  tr.appendChild(td);
  afterTr.parentNode.insertBefore(tr, afterTr.nextSibling);
}

function toggleCommentRow(lineNo, tr) {
  const next = tr.nextSibling;
  if (next && next.classList && next.classList.contains('comments-row')) {
    // already showing — just add a form if not present
    if (!next.querySelector('.comment-form')) {
      next.remove();
      appendCommentsRow(tr, lineNo, true);
    } else {
      next.remove();
      if ((COMMENTS[String(lineNo)]||[]).length) appendCommentsRow(tr, lineNo, false);
    }
  } else {
    appendCommentsRow(tr, lineNo, true);
  }
}

async function init() {
  const tree = await api('/api/tree');
  renderTree(tree, document.getElementById('tree'));
  await loadChanged();
}
init();
</script>
</body>
</html>
"""


class Handler(BaseHTTPRequestHandler):
    def log_message(self, fmt, *args):
        sys.stderr.write("[%s] %s\n" % (time.strftime("%H:%M:%S"), fmt % args))

    def _send_json(self, obj, status=200):
        body = json.dumps(obj).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def _send_html(self, html):
        body = html.encode("utf-8")
        self.send_response(200)
        self.send_header("Content-Type", "text/html; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        u = urllib.parse.urlparse(self.path)
        q = urllib.parse.parse_qs(u.query)
        try:
            if u.path == "/" or u.path == "/index.html":
                self._send_html(INDEX_HTML)
            elif u.path == "/api/tree":
                self._send_json(build_tree())
            elif u.path == "/api/changed":
                self._send_json(changed_files())
            elif u.path == "/api/file":
                rel = (q.get("path") or [""])[0]
                full = safe_path(rel)
                if not full or not os.path.isfile(full) or not full.endswith(".cs"):
                    self._send_json({"error": "not found"}, 404); return
                with open(full, "r", encoding="utf-8", errors="replace") as f:
                    content = f.read()
                cmts = load_comments().get(rel, {})
                self._send_json({
                    "path": rel,
                    "content": content,
                    "session_lines": session_changed_lines(rel),
                    "branch_lines": branch_changed_lines(rel),
                    "comments": cmts,
                })
            else:
                self.send_response(404); self.end_headers()
        except Exception as e:
            self._send_json({"error": str(e)}, 500)

    def do_POST(self):
        u = urllib.parse.urlparse(self.path)
        try:
            if u.path == "/api/comments":
                length = int(self.headers.get("Content-Length", "0"))
                data = json.loads(self.rfile.read(length).decode("utf-8"))
                rel = data["path"]; line = int(data["line"]); text = data["text"]
                if not safe_path(rel):
                    self._send_json({"error": "bad path"}, 400); return
                all_c = load_comments()
                file_c = all_c.setdefault(rel, {})
                lst = file_c.setdefault(str(line), [])
                lst.append({"ts": int(time.time()), "text": text})
                save_comments(all_c)
                self._send_json({"ok": True, "comments": file_c})
            else:
                self.send_response(404); self.end_headers()
        except Exception as e:
            self._send_json({"error": str(e)}, 500)


def main():
    print(f"PLang C# Review UI → http://localhost:{PORT}")
    print(f"Root:     {ROOT}")
    print(f"Comments: {COMMENTS_FILE}")
    srv = ThreadingHTTPServer(("0.0.0.0", PORT), Handler)
    try:
        srv.serve_forever()
    except KeyboardInterrupt:
        print("\nShutting down.")
        srv.server_close()


if __name__ == "__main__":
    main()
