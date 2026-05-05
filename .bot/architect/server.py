#!/usr/bin/env python3
"""
Architect review server — view current-branch architect output, comment per line.

Run:   python3 .bot/architect/server.py
Open:  http://localhost:8081

Auto-detects the current git branch and serves files under
.bot/<branch>/architect/  (all *.md, recursive).

Comments are saved to .bot/<branch>/architect/comments.json
so they live with the branch and get committed alongside the work.
"""

import http.server
import json
import os
import subprocess
import urllib.parse
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
    files = []
    for p in sorted(base.rglob("*.md")):
        files.append(str(p.relative_to(base)))
    # Sort: root summary.md first, then v1/, v2/, ... in numeric order
    def sort_key(rel: str):
        parts = rel.split("/")
        if len(parts) == 1:
            return (0, 0, rel)
        v = parts[0]
        n = int(v[1:]) if v.startswith("v") and v[1:].isdigit() else 999
        return (1, n, rel)
    files.sort(key=sort_key)
    return files


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


INDEX_HTML = r"""<!doctype html>
<html><head><meta charset="utf-8"><title>Architect Review — {branch}</title>
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
  #main { flex:1; overflow-y:auto; padding:0; }
  #header { padding:12px 20px; background:#2d2d30; border-bottom:1px solid #333; position:sticky; top:0; z-index:10; }
  #header h1 { margin:0; font-size:14px; font-family:monospace; color:#9cdcfe; }
  #content { padding:20px; }
  .line-row { display:flex; font-family: 'SF Mono', Consolas, monospace; font-size:13px; line-height:1.5; }
  .line-row:hover { background:#2a2d2e; }
  .line-row:hover .ln { color:#888; }
  .ln { width:50px; text-align:right; padding-right:12px; color:#555; user-select:none; cursor:pointer; flex-shrink:0; }
  .ln:hover { color:#fff !important; background:#094771; }
  .ln.has-comment { color:#f48771; font-weight:bold; }
  .lc { white-space:pre-wrap; word-break:break-word; flex:1; padding-right:12px; }
  .comment-block { margin: 4px 0 4px 50px; padding:10px 12px; background:#2d3a4e; border-left:3px solid #569cd6; border-radius:3px; font-family:-apple-system,system-ui,sans-serif; font-size:13px; }
  .comment-block .meta { color:#888; font-size:11px; margin-bottom:4px; }
  .comment-block .body { white-space:pre-wrap; }
  .comment-block .del { float:right; color:#f48771; cursor:pointer; font-size:11px; }
  #composer { display:none; margin: 4px 0 4px 50px; padding:10px; background:#252526; border:1px solid #569cd6; border-radius:3px; }
  #composer textarea { width:100%; min-height:70px; background:#1e1e1e; color:#d4d4d4; border:1px solid #444; padding:6px; font-family:inherit; font-size:13px; resize:vertical; }
  #composer .row { margin-top:6px; display:flex; gap:8px; align-items:center; }
  #composer button { background:#0e639c; color:#fff; border:0; padding:6px 14px; cursor:pointer; border-radius:2px; font-size:13px; }
  #composer button.cancel { background:#3a3d41; }
  #composer .target { color:#888; font-size:11px; flex:1; }
  .empty { padding:40px; color:#888; text-align:center; }
</style></head>
<body>
<div id="sidebar">
  <h2>Architect Output</h2>
  <div class="branch">branch: {branch}</div>
  <ul id="filelist"></ul>
</div>
<div id="main">
  <div id="header"><h1 id="filename">Select a file</h1></div>
  <div id="content"><div class="empty">Pick a file from the left.</div></div>
</div>

<script>
let currentFile = null;
let comments = {};
let files = [];

async function loadFiles() {
  const r = await fetch('/api/files');
  const j = await r.json();
  files = j.files;
  comments = j.comments;
  renderSidebar();
  if (files.length && !currentFile) openFile(files[0]);
}

function renderSidebar() {
  const ul = document.getElementById('filelist');
  ul.innerHTML = '';
  for (const f of files) {
    const li = document.createElement('li');
    li.textContent = f;
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

async function openFile(path) {
  currentFile = path;
  document.getElementById('filename').textContent = path;
  const r = await fetch('/api/file?path=' + encodeURIComponent(path));
  const j = await r.json();
  renderFile(j.lines);
  renderSidebar();
}

function renderFile(lines) {
  const c = document.getElementById('content');
  c.innerHTML = '';
  const fileComments = comments[currentFile] || [];
  const byLine = {};
  for (const cm of fileComments) {
    (byLine[cm.line] ||= []).push(cm);
  }
  lines.forEach((text, i) => {
    const lineNo = i + 1;
    const row = document.createElement('div');
    row.className = 'line-row';
    const ln = document.createElement('div');
    ln.className = 'ln';
    if (byLine[lineNo]) ln.classList.add('has-comment');
    ln.textContent = lineNo;
    ln.onclick = () => showComposer(lineNo);
    const lc = document.createElement('div');
    lc.className = 'lc';
    lc.textContent = text || ' ';
    row.appendChild(ln); row.appendChild(lc);
    c.appendChild(row);
    if (byLine[lineNo]) {
      for (const cm of byLine[lineNo]) {
        const cb = document.createElement('div');
        cb.className = 'comment-block';
        const meta = document.createElement('div');
        meta.className = 'meta';
        meta.textContent = '@ line ' + lineNo + '  ·  ' + cm.ts;
        const del = document.createElement('span');
        del.className = 'del'; del.textContent = '✕ delete';
        del.onclick = () => deleteComment(cm.id);
        meta.appendChild(del);
        const body = document.createElement('div');
        body.className = 'body'; body.textContent = cm.text;
        cb.appendChild(meta); cb.appendChild(body);
        c.appendChild(cb);
      }
    }
  });
}

function showComposer(lineNo) {
  let comp = document.getElementById('composer');
  if (comp) comp.remove();
  comp = document.createElement('div');
  comp.id = 'composer';
  comp.style.display = 'block';
  comp.innerHTML = `
    <textarea id="cmt-text" placeholder="Comment on line ${lineNo}..."></textarea>
    <div class="row">
      <span class="target">${currentFile} : line ${lineNo}</span>
      <button class="cancel" onclick="document.getElementById('composer').remove()">Cancel</button>
      <button onclick="submitComment(${lineNo})">Save</button>
    </div>`;
  // Insert composer just below the clicked line
  const rows = document.querySelectorAll('#content .line-row');
  if (rows[lineNo - 1]) {
    rows[lineNo - 1].insertAdjacentElement('afterend', comp);
  } else {
    document.getElementById('content').appendChild(comp);
  }
  document.getElementById('cmt-text').focus();
}

async function submitComment(lineNo) {
  const text = document.getElementById('cmt-text').value.trim();
  if (!text) return;
  const r = await fetch('/api/comment', {
    method: 'POST',
    headers: {'Content-Type':'application/json'},
    body: JSON.stringify({ file: currentFile, line: lineNo, text })
  });
  const j = await r.json();
  comments = j.comments;
  await openFile(currentFile);
}

async function deleteComment(id) {
  if (!confirm('Delete this comment?')) return;
  const r = await fetch('/api/comment?id=' + encodeURIComponent(id), { method: 'DELETE' });
  const j = await r.json();
  comments = j.comments;
  await openFile(currentFile);
}

loadFiles();
</script>
</body></html>
"""


class Handler(http.server.BaseHTTPRequestHandler):
    def log_message(self, fmt, *args):
        pass  # quiet

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
        if u.path == "/" or u.path == "/index.html":
            html = INDEX_HTML.replace("{branch}", current_branch())
            self._send(200, html, "text/html")
            return
        if u.path == "/api/files":
            self._json(200, {"files": list_files(), "comments": load_comments()})
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
            self._json(200, {"lines": text.splitlines()})
            return
        if u.path == "/api/comment" and self.command == "DELETE":
            return self.do_DELETE()
        self._send(404, "not found", "text/plain")

    def do_DELETE(self):
        u = urllib.parse.urlparse(self.path)
        if u.path != "/api/comment":
            self._send(404, "not found", "text/plain"); return
        cid = urllib.parse.parse_qs(u.query).get("id", [""])[0]
        comments = load_comments()
        for f, arr in list(comments.items()):
            comments[f] = [c for c in arr if c.get("id") != cid]
            if not comments[f]:
                del comments[f]
        save_comments(comments)
        self._json(200, {"comments": comments})

    def do_POST(self):
        u = urllib.parse.urlparse(self.path)
        if u.path != "/api/comment":
            self._send(404, "not found", "text/plain"); return
        length = int(self.headers.get("Content-Length", "0"))
        body = json.loads(self.rfile.read(length).decode("utf-8"))
        f = body["file"]; line = int(body["line"]); text = body["text"]
        import uuid, datetime
        entry = {
            "id": uuid.uuid4().hex[:10],
            "line": line,
            "text": text,
            "ts": datetime.datetime.now().isoformat(timespec="seconds"),
        }
        comments = load_comments()
        comments.setdefault(f, []).append(entry)
        comments[f].sort(key=lambda c: c["line"])
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
