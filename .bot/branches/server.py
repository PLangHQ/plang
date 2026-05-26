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
from pathlib import Path

PORT = 8083
REPO_ROOT = Path(__file__).resolve().parents[2]
BOT = REPO_ROOT / ".bot"

# Canonical pipeline order. Anything else gets bucketed as "other" at the end.
PIPELINE = [
    "architect",
    "codeanalyzer",
    "builder",
    "coder",
    "tester",
    "test-designer",
    "auditor",
    "security",
    "docs",
    "handoff",
    "learnings",
    "scaffolder",
]
KNOWN_ROLES = set(PIPELINE)

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


def git_branches() -> set[str]:
    """Set of branch names known to git (local + remote, slashes -> dashes)."""
    try:
        out = subprocess.check_output(
            ["git", "-C", str(REPO_ROOT), "for-each-ref",
             "--format=%(refname:short)", "refs/heads", "refs/remotes"]
        ).decode().splitlines()
    except Exception:
        return set()
    names = set()
    for ref in out:
        ref = ref.strip()
        if not ref:
            continue
        if ref.startswith("origin/"):
            ref = ref[len("origin/"):]
        names.add(ref.replace("/", "-"))
    return names


def is_branch_dir(p: Path) -> bool:
    """A branch dir has at least one subdir whose name is a known role."""
    if not p.is_dir() or p.name in NON_BRANCH:
        return False
    for child in p.iterdir():
        if child.is_dir() and child.name in KNOWN_ROLES:
            return True
    return False


def dir_mtime_recursive(p: Path) -> float:
    """Max mtime over all files in p, recursively. 0 if empty."""
    mt = 0.0
    try:
        if p.is_file():
            return p.stat().st_mtime
        for child in p.rglob("*"):
            try:
                if child.is_file():
                    t = child.stat().st_mtime
                    if t > mt:
                        mt = t
            except OSError:
                pass
    except OSError:
        pass
    return mt


def role_summary(role_dir: Path) -> dict:
    files = []
    for f in sorted(role_dir.rglob("*")):
        if f.is_file():
            try:
                files.append({
                    "path": str(f.relative_to(role_dir)),
                    "mtime": f.stat().st_mtime,
                    "size": f.stat().st_size,
                })
            except OSError:
                pass
    return {
        "mtime": dir_mtime_recursive(role_dir),
        "files": files,
    }


def scan_branches() -> list[dict]:
    if not BOT.exists():
        return []
    live = git_branches()
    cur = current_branch()
    branches = []
    for p in BOT.iterdir():
        if not is_branch_dir(p):
            continue
        roles = {}
        other = {}
        for sub in p.iterdir():
            if not sub.is_dir():
                continue
            entry = role_summary(sub)
            if sub.name in KNOWN_ROLES:
                roles[sub.name] = entry
            else:
                other[sub.name] = entry
        root_files = []
        for f in sorted(p.iterdir()):
            if f.is_file():
                try:
                    root_files.append({
                        "name": f.name,
                        "mtime": f.stat().st_mtime,
                        "size": f.stat().st_size,
                    })
                except OSError:
                    pass
        branches.append({
            "name": p.name,
            "lastActivity": dir_mtime_recursive(p),
            "inGit": p.name in live,
            "isCurrent": p.name == cur,
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
  .branch .meta .gitstate.archived { color: var(--warn); }
  .branch .roles { margin-top: 4px; display: flex; gap: 2px; flex-wrap: wrap; }
  .role-dot {
    width: 10px; height: 10px; border-radius: 2px;
    background: var(--pending);
  }
  .role-dot.done { background: var(--done); }
  #pane {
    flex: 1;
    padding: 20px 28px;
    overflow-y: auto;
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
  }
  #pane .subtitle { color: var(--muted); margin-bottom: 24px; display: flex; gap: 12px; flex-wrap: wrap; }
  #pane .subtitle .pill {
    padding: 1px 8px; border-radius: 10px; background: var(--panel2);
    border: 1px solid var(--border);
  }
  #pane .subtitle .pill.current { color: var(--current); border-color: var(--current); }
  #pane .subtitle .pill.archived { color: var(--warn); border-color: var(--warn); }
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
  .stage.empty { opacity: 0.45; }
  .stage .name { font-weight: 500; }
  .stage .icon { text-align: center; font-size: 14px; }
  .stage .icon.done { color: var(--done); }
  .stage .icon.empty { color: var(--pending); }
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

<script>
const PIPELINE = ["architect","codeanalyzer","builder","coder","tester","test-designer","auditor","security","docs","handoff","learnings","scaffolder"];
let BRANCHES = [];
let SELECTED = null;

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

function fmtSize(b) {
  if (b < 1024) return b + "B";
  if (b < 1024 * 1024) return (b / 1024).toFixed(1) + "K";
  return (b / 1024 / 1024).toFixed(1) + "M";
}

function renderNav(filter) {
  const list = document.getElementById("branchList");
  const f = (filter || "").toLowerCase();
  const visible = BRANCHES.filter(b => !f || b.name.toLowerCase().includes(f));
  document.getElementById("count").textContent = visible.length + " / " + BRANCHES.length;
  list.innerHTML = "";
  for (const b of visible) {
    const div = document.createElement("div");
    div.className = "branch" + (b.isCurrent ? " current" : "") + (SELECTED === b.name ? " active" : "");
    const gitState = b.inGit ? "" : '<span class="gitstate archived">archived</span>';
    const dots = PIPELINE.map(r => `<div class="role-dot${b.roles[r] ? " done" : ""}" title="${r}"></div>`).join("");
    div.innerHTML = `
      <div class="name">${b.name}</div>
      <div class="meta"><span>${fmtTime(b.lastActivity)}</span>${gitState}</div>
      <div class="roles">${dots}</div>
    `;
    div.onclick = () => { SELECTED = b.name; renderNav(filter); renderPane(b); };
    list.appendChild(div);
  }
}

function renderPane(b) {
  const pane = document.getElementById("pane");
  const pills = [];
  if (b.isCurrent) pills.push('<span class="pill current">current branch</span>');
  pills.push(`<span class="pill">${b.inGit ? "in git" : "archived"}</span>`);
  pills.push(`<span class="pill">last activity ${fmtTime(b.lastActivity)}</span>`);

  const stages = PIPELINE.map(role => {
    const r = b.roles[role];
    const has = !!r;
    const filesCount = has ? r.files.length : 0;
    const fileList = has && r.files.length
      ? `<details><summary>${filesCount} file${filesCount === 1 ? "" : "s"}</summary><ul class="filelist">${r.files.map(f => `<li>${f.path} <span style="float:right">${fmtSize(f.size)} · ${fmtTime(f.mtime)}</span></li>`).join("")}</ul></details>`
      : `<span class="files">—</span>`;
    return `
      <div class="stage${has ? "" : " empty"}">
        <div class="name">${role}</div>
        <div class="icon ${has ? "done" : "empty"}">${has ? "●" : "○"}</div>
        <div>${fileList}</div>
        <div class="time">${has ? fmtTime(r.mtime) : ""}</div>
      </div>
    `;
  }).join("");

  const otherRoles = Object.entries(b.other || {});
  const otherSection = otherRoles.length ? `
    <h2 class="section">Other subdirs</h2>
    <div class="timeline">
      ${otherRoles.map(([name, r]) => `
        <div class="stage">
          <div class="name">${name}</div>
          <div class="icon done">●</div>
          <div><details><summary>${r.files.length} file${r.files.length === 1 ? "" : "s"}</summary><ul class="filelist">${r.files.map(f => `<li>${f.path} <span style="float:right">${fmtSize(f.size)} · ${fmtTime(f.mtime)}</span></li>`).join("")}</ul></details></div>
          <div class="time">${fmtTime(r.mtime)}</div>
        </div>
      `).join("")}
    </div>
  ` : "";

  const rootSection = (b.rootFiles || []).length ? `
    <h2 class="section">Root files</h2>
    <ul class="flat">
      ${b.rootFiles.map(f => `<li><span>${f.name}</span><span class="ts">${fmtSize(f.size)} · ${fmtTimeAbs(f.mtime)}</span></li>`).join("")}
    </ul>
  ` : "";

  pane.innerHTML = `
    <h1>${b.name}</h1>
    <div class="subtitle">${pills.join("")}</div>
    <h2 class="section">Pipeline</h2>
    <div class="timeline">${stages}</div>
    ${otherSection}
    ${rootSection}
  `;
}

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
