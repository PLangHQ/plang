#!/usr/bin/env python3
"""Dev server for the builder trace viewer.

Scans one or more root paths for every app's trace directory
(``<root>/**/.build/traces/``). The current layout has one folder per build
run (``<traces>/<trace.id>/``) containing per-goal JSON files
(``<traces>/<trace.id>/<GoalName>.json``) plus a manifest. Trace IDs sort
chronologically by their leading ticks.

A trace is addressed by ``<bucket>/<trace.id>/<filename>``, where ``<bucket>``
is the relative path from one of the roots to the .build/traces/ directory's
parent app (e.g. ``system/builder``). Same goal name across different build
runs lives under different trace.id folders, so the full ID is always unique.

Usage:
    python3 server.py                     # defaults: plang root + tests/
    python3 server.py --port 8080
    python3 server.py --root /path/a --root /path/b
    python3 server.py /path/a /path/b     # positional roots (port defaults to 8080)
"""

import argparse
import http.server
import json
import os
import socketserver
import sys

VIEWER_DIR = os.path.abspath(os.path.dirname(__file__))
# STATIC_ROOT is the directory the HTTP handler serves files from. Points at
# os/ so URLs like /system/builder/web/index.html resolve relative to it.
STATIC_ROOT = os.path.abspath(os.path.join(VIEWER_DIR, '..', '..', '..'))
# REPO_ROOT is the actual repo top — one level above os/. Used for the default
# trace-discovery roots so both system traces (under os/) and user-app traces
# (under Tests/) are picked up without --root flags.
REPO_ROOT = os.path.abspath(os.path.join(VIEWER_DIR, '..', '..', '..', '..'))
PLANG_ROOT = STATIC_ROOT  # back-compat alias

# Display caps.
# - MAX_PATHS_TOTAL: keep the (bucket, goalName) groups whose newest trace is
#   among the most recent. Was 20 — too small for the sidebar search box,
#   which can only filter what's already returned. Raised to a large value
#   that effectively removes the cap for typical test corpuses (~200 goals)
#   while still bounding the worst case. The Recent panel still slices its
#   own newest 20 from this larger pool.
# - MAX_TRACES_PER_GOAL: within each group, at most this many recent traces
#   (= different build runs that produced a trace for this goal). Stops a hot
#   iteration cycle on one goal from burying everything else in tabs.
MAX_PATHS_TOTAL = 2000
MAX_TRACES_PER_GOAL = 3

# Summary cache: path → (mtime, summary_dict). Parsing every trace (some 40KB+)
# on every /api/traces call pushed load time to 15s with 800+ traces. Re-parse
# only when the file's mtime changed.
_SUMMARY_CACHE: dict = {}

# --- CLI ---

def _parse_args():
    p = argparse.ArgumentParser(description='PLang builder trace viewer server.')
    p.add_argument('--port', type=int, default=8080)
    p.add_argument('--root', action='append', default=[],
                   help='Root path to scan for .build/traces/ dirs (repeatable).')
    p.add_argument('positional', nargs='*',
                   help='Additional root paths (positional). If --port is an int '
                        'as the first positional (for back-compat), it is treated '
                        'as the port.')
    args = p.parse_args()

    roots = [os.path.abspath(r) for r in args.root]
    for r in args.positional:
        if r.isdigit() and args.port == 8080 and not args.root:
            # Legacy: `python3 server.py 9000` → port 9000
            args.port = int(r)
        else:
            roots.append(os.path.abspath(r))

    if not roots:
        # Default: the actual repo root (parent of os/) so both system traces
        # under os/system/builder/ and user-app traces under Tests/ are picked
        # up without --root flags. Tests/ is added explicitly because some OSes
        # are case-sensitive and a generic repo-walk should still find it
        # quickly when it's the deepest scan target.
        roots = [REPO_ROOT, os.path.join(REPO_ROOT, 'Tests')]

    # Dedupe while preserving order.
    seen, unique = set(), []
    for r in roots:
        if r not in seen:
            seen.add(r)
            unique.append(r)
    return args.port, unique

PORT, ROOTS = _parse_args()


# --- Trace discovery ---

def _is_trace_id(name: str) -> bool:
    """trace.id folder names are ``<ticks>_<guid8>`` — tick-prefixed and
    sortable. Reject anything else (legacy top-level files, stray dirs)."""
    if '_' not in name:
        return False
    return name.split('_', 1)[0].isdigit()


def _bucket_label(traces_dir: str) -> str:
    """Name the bucket by the app's path relative to the nearest root.

    ``traces_dir`` is ``<app>/.build/traces`` — strip ``.build/traces`` to get
    the app dir, then make it relative to a known root.
    """
    app_dir = os.path.dirname(os.path.dirname(traces_dir))
    for root in ROOTS:
        try:
            rel = os.path.relpath(app_dir, root)
        except ValueError:
            continue
        if not rel.startswith('..'):
            return rel.replace(os.sep, '/') if rel != '.' else '(root)'
    return app_dir.replace(os.sep, '/')


def _find_trace_dirs():
    """Yield every ``<app>/.build/traces`` directory across the configured roots.

    The traces dir is the *parent* of per-build folders; each build gets its
    own ``<trace.id>/`` subfolder containing the goal JSONs.
    """
    seen = set()
    for root in ROOTS:
        if not os.path.isdir(root):
            continue
        for dirpath, dirnames, _ in os.walk(root, followlinks=True):
            dirnames[:] = [d for d in dirnames if d not in ('.git', 'node_modules', 'bin', 'obj')]
            if os.path.basename(dirpath) == 'traces' and os.path.basename(os.path.dirname(dirpath)) == '.build':
                real = os.path.realpath(dirpath)
                if real in seen:
                    continue
                seen.add(real)
                yield dirpath


def _summarize_trace(full_path: str) -> dict | None:
    """Read just the fields the sidebar tree needs. Stream-parse so giant
    (42KB+) system prompts aren't kept in memory for every listing. Returns
    None when the file is empty/unparseable — caller drops the entry instead
    of showing a ghost row with empty goal/path (which the viewer's sort
    callbacks then choke on)."""
    try:
        with open(full_path, encoding='utf-8') as f:
            raw = f.read()
        if not raw.strip():
            return None
        data = json.loads(raw, strict=False)
    except (OSError, json.JSONDecodeError):
        return None
    if not isinstance(data, dict):
        return None
    # v3 trace shape: top-level `goal` (object, PascalCase keys), `plan`, and
    # `stepPasses` (per-step compile responses). v1 shape had `pass1.response`
    # with lower-case `steps/errors/warnings` and a string `goal` — keep the
    # fallback so old traces still render.
    goal_obj = data.get('goal')
    if isinstance(goal_obj, dict):
        # PascalCase from the new Goal serializer; lowercase fallback for any
        # in-between traces that still used the old field names.
        goal_name = goal_obj.get('Name') or goal_obj.get('name') or ''
        goal_path = goal_obj.get('Path') or goal_obj.get('path') or ''
        goal_vis = goal_obj.get('Visibility') or goal_obj.get('visibility') or ''
        goal_steps = goal_obj.get('Steps') or goal_obj.get('steps') or []
    else:
        goal_name = goal_obj or ''
        goal_path = data.get('path', '')
        goal_vis = data.get('visibility', '')
        goal_steps = []

    # stepCount: use the source goal's step count when present (matches what
    # the user sees in the .goal file). Fall back to legacy pass1.response.
    if goal_steps:
        steps = goal_steps
    else:
        legacy_resp = (data.get('pass1') or {}).get('response') or {}
        steps = legacy_resp.get('steps') if isinstance(legacy_resp.get('steps'), list) else []

    # Errors/warnings aggregate across v3 stepPasses (each entry's
    # value.response carries the compile-time errors/warnings) and the legacy
    # pass1.response slot. The .value wrapper is the data envelope from the
    # builder pipeline — older v3 traces nest the response directly.
    errors_count = 0
    warnings_count = 0
    # Token + cost aggregates: planner usage + every stepPass usage, summed.
    # Cost may be null (unknown model); skip those entries to avoid 0 + None.
    prompt_tokens = 0
    cached_tokens = 0
    completion_tokens = 0
    total_tokens = 0
    cost_sum = 0.0
    cost_seen = False
    plan_usage = (data.get('plan') or {}).get('usage') if isinstance(data.get('plan'), dict) else None
    if isinstance(plan_usage, dict):
        prompt_tokens     += int(plan_usage.get('promptTokens') or 0)
        cached_tokens     += int(plan_usage.get('cachedTokens') or 0)
        completion_tokens += int(plan_usage.get('completionTokens') or 0)
        total_tokens      += int(plan_usage.get('totalTokens') or 0)
        if plan_usage.get('cost') is not None:
            cost_sum += float(plan_usage['cost']); cost_seen = True
    step_passes = data.get('stepPasses')
    if isinstance(step_passes, list):
        for sp in step_passes:
            if not isinstance(sp, dict): continue
            payload = sp.get('value') if isinstance(sp.get('value'), dict) else sp
            resp = payload.get('response') if isinstance(payload.get('response'), dict) else {}
            errors_count += len(resp.get('errors') or [])
            warnings_count += len(resp.get('warnings') or [])
            usage = payload.get('usage') if isinstance(payload.get('usage'), dict) else None
            if usage:
                prompt_tokens     += int(usage.get('promptTokens') or 0)
                cached_tokens     += int(usage.get('cachedTokens') or 0)
                completion_tokens += int(usage.get('completionTokens') or 0)
                total_tokens      += int(usage.get('totalTokens') or 0)
                if usage.get('cost') is not None:
                    cost_sum += float(usage['cost']); cost_seen = True
    legacy_resp = (data.get('pass1') or {}).get('response') or {}
    if isinstance(legacy_resp, dict):
        errors_count += len(legacy_resp.get('errors') or [])
        warnings_count += len(legacy_resp.get('warnings') or [])
    return {
        'goal': goal_name,
        'path': goal_path,
        'visibility': goal_vis,
        'timestamp': data.get('timestamp', ''),
        'stepCount': len(steps),
        'errors': errors_count,
        'warnings': warnings_count,
        'buildError': bool(data.get('buildError')),
        'usage': {
            'promptTokens': prompt_tokens,
            'cachedTokens': cached_tokens,
            'completionTokens': completion_tokens,
            'totalTokens': total_tokens,
            'cost': cost_sum if cost_seen else None,
        },
    }


def _list_all_traces(with_summary: bool = True):
    """Gather traces across every bucket under the new per-build folder layout.

    Each entry has {id, bucket, traceId, filename, goalName, mtime, fullPath}.
    With summary, the tree-render fields are inlined so the client builds the
    sidebar without a per-trace round trip.

    Caps applied (newest-first by trace.id):
    - Per (bucket, goalName) group: keep at most MAX_TRACES_PER_GOAL traces.
    - Globally: keep at most MAX_PATHS_TOTAL distinct (bucket, goalName) groups —
      newest-by-most-recent-trace wins. The sidebar's goal-path count caps
      here, regardless of how many builds touched each goal.
    """
    all_entries = []
    for traces_dir in _find_trace_dirs():
        bucket = _bucket_label(traces_dir)
        try:
            build_folders = os.listdir(traces_dir)
        except OSError:
            continue
        for trace_id in build_folders:
            if not _is_trace_id(trace_id):
                continue
            build_dir = os.path.join(traces_dir, trace_id)
            if not os.path.isdir(build_dir):
                continue
            try:
                files = os.listdir(build_dir)
            except OSError:
                continue
            for name in files:
                # Goal traces are <GoalName>.json; skip manifest and the llm/
                # subfolder's .txt LLM-debug captures.
                if not name.endswith('.json'):
                    continue
                if name == 'manifest.json':
                    continue
                full = os.path.join(build_dir, name)
                if not os.path.isfile(full):
                    continue
                try:
                    mtime = os.path.getmtime(full)
                except OSError:
                    continue
                all_entries.append({
                    'id': f'{bucket}/{trace_id}/{name}',
                    'bucket': bucket,
                    'traceId': trace_id,
                    'filename': name,
                    'goalName': name[:-5],  # strip .json
                    'mtime': mtime,
                    'fullPath': full,
                })

    # Group by (bucket, goalPath); each group's traces sorted newest-first.
    # Keying on the goal's SOURCE PATH (not the trace filename) is what lets
    # the sidebar show distinct rows for PLang test goals — every test goal is
    # named "Start", so its trace file is "Start.json", and a filename-keyed
    # grouping collapses all of them into one row. The path
    # (/TypeKindStrict/SetAsText...test.goal) is unique per goal. Summaries are
    # cached by (fullPath, mtime), so the parse needed to read the path
    # amortizes to ~0 across calls.
    from collections import defaultdict

    def _group_key(e):
        cached = _SUMMARY_CACHE.get(e['fullPath'])
        if cached is not None and cached[0] == e['mtime']:
            s = cached[1]
        else:
            s = _summarize_trace(e['fullPath'])
            _SUMMARY_CACHE[e['fullPath']] = (e['mtime'], s)
        path = (s or {}).get('path') if s else None
        # Fall back to the filename-derived name when a trace has no path
        # (legacy/empty traces) so they still group sanely.
        return (e['bucket'], path or e['goalName'])

    by_goal = defaultdict(list)
    for e in all_entries:
        by_goal[_group_key(e)].append(e)
    for entries in by_goal.values():
        entries.sort(key=lambda e: e['traceId'], reverse=True)

    # Global cap on distinct goal-paths: keep the MAX_PATHS_TOTAL groups whose
    # newest trace is most recent. Goals that haven't been touched in a while
    # drop off the sidebar even if older traces still exist on disk — EXCEPT
    # when the group's newest trace carries warnings, errors, or a buildError:
    # those are always surfaced so the webui's errors/warnings filter has the
    # full set to filter over, regardless of recency. Summary lookup is cached
    # by fullPath+mtime so the extra parse per excluded group amortizes to ~0.
    all_sorted = sorted(by_goal.values(),
                        key=lambda entries: entries[0]['traceId'],
                        reverse=True)
    def _head_has_issue(entries):
        head = entries[0] if entries else None
        if not head: return False
        cached = _SUMMARY_CACHE.get(head['fullPath'])
        if cached is not None and cached[0] == head['mtime']:
            s = cached[1]
        else:
            s = _summarize_trace(head['fullPath'])
            _SUMMARY_CACHE[head['fullPath']] = (head['mtime'], s)
        if s is None: return False
        return bool(s.get('errors', 0) or s.get('warnings', 0) or s.get('buildError'))
    top_recent = all_sorted[:MAX_PATHS_TOTAL]
    # Append any group with issues that didn't make the top cut.
    for g in all_sorted[MAX_PATHS_TOTAL:]:
        if _head_has_issue(g):
            top_recent.append(g)
    groups = top_recent

    # Per-goal cap inside each surviving group, then flatten newest-first.
    capped = []
    for entries in groups:
        capped.extend(entries[:MAX_TRACES_PER_GOAL])
    capped.sort(key=lambda e: e['traceId'], reverse=True)

    if with_summary:
        enriched = []
        for t in capped:
            cached = _SUMMARY_CACHE.get(t['fullPath'])
            if cached is not None and cached[0] == t['mtime']:
                summary = cached[1]
            else:
                summary = _summarize_trace(t['fullPath'])
                _SUMMARY_CACHE[t['fullPath']] = (t['mtime'], summary)
            if summary is None:
                continue
            t.update(summary)
            enriched.append(t)
        return enriched

    return capped


def _resolve_trace_id(trace_id: str):
    """Turn a client-supplied ``<bucket>/<trace.id>/<filename>`` back into an
    absolute file. Defends against traversal — any ``..`` segment is rejected.
    """
    if '..' in trace_id.split('/') or not trace_id:
        return None
    # Skip summary parse — we only need the path, not every trace's body.
    for t in _list_all_traces(with_summary=False):
        if t['id'] == trace_id:
            return t['fullPath']
    return None


# --- HTTP handler ---

class Handler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        # Static serving directory is PLANG_ROOT so any app's .goal source files
        # resolve when the viewer renders `trace.path` as a link.
        super().__init__(*args, directory=PLANG_ROOT, **kwargs)

    def end_headers(self):
        # Disable caching for every response. During active development the
        # viewer's HTML/JS changes frequently — a stale browser cache is what
        # caused the earlier "clicking a tree row blanks the panel" confusion.
        self.send_header('Cache-Control', 'no-store, max-age=0')
        super().end_headers()

    def do_GET(self):
        if self.path == '/api/traces':
            self.send_trace_list()
        elif self.path.startswith('/api/traces/'):
            self.send_trace_file(self.path[len('/api/traces/'):])
        elif self.path == '/api/test-results':
            self.send_test_results()
        else:
            super().do_GET()

    def send_test_results(self):
        """Scan every `*/.test/results.json` under PLANG_ROOT and return a dict
        keyed by bucket (the directory under PLANG_ROOT that contains `.test/`).
        Client looks up `byBucket[file.bucket]` and finds the run whose `path`
        matches `file.path`. Tiny files (~50KB for 200 tests); no caching yet."""
        out = {}
        # Walk REPO_ROOT (repo top), not PLANG_ROOT (os/) — Tests/.test/ lives
        # at the repo top alongside os/, so the os/-rooted walk misses it.
        for dirpath, dirnames, filenames in os.walk(REPO_ROOT):
            dirnames[:] = [d for d in dirnames if d not in ('.git', 'node_modules', 'bin', 'obj', '.build')]
            if os.path.basename(dirpath) != '.test':
                continue
            results_file = os.path.join(dirpath, 'results.json')
            if not os.path.isfile(results_file):
                continue
            bucket_root = os.path.dirname(dirpath)
            bucket = os.path.relpath(bucket_root, REPO_ROOT).replace(os.sep, '/')
            if bucket == '.':
                bucket = ''
            try:
                with open(results_file, 'r', encoding='utf-8') as f:
                    out[bucket] = {
                        'mtime': os.path.getmtime(results_file),
                        'data': json.load(f),
                    }
            except Exception as e:
                out[bucket] = {'mtime': 0, 'error': str(e)}
        self.send_json(out)

    def send_trace_list(self):
        traces = _list_all_traces(with_summary=True)
        # Send summary fields inline so the client builds the sidebar tree
        # without fetching every trace body. Full payloads load lazily when a
        # specific trace is selected (see send_trace_file below).
        self.send_json([
            {
                'id': t['id'], 'bucket': t['bucket'],
                'traceId': t['traceId'], 'filename': t['filename'],
                'goalName': t['goalName'],
                'goal': t.get('goal', ''), 'path': t.get('path', ''),
                'visibility': t.get('visibility', ''), 'timestamp': t.get('timestamp', ''),
                'stepCount': t.get('stepCount', 0),
                'errors': t.get('errors', 0),
                'warnings': t.get('warnings', 0),
                'buildError': t.get('buildError', False),
                'usage': t.get('usage') or {},
            }
            for t in traces
        ])

    def send_trace_file(self, trace_id):
        # URL-decode (%2F etc.) before resolving.
        try:
            from urllib.parse import unquote
            trace_id = unquote(trace_id)
        except Exception:
            pass
        path = _resolve_trace_id(trace_id)
        if path is None or not os.path.isfile(path):
            self.send_error(404)
            return
        try:
            with open(path, encoding='utf-8') as f:
                raw = f.read()
            data = json.loads(raw, strict=False)
            # Stamp bucket so the UI can show which app the trace came from
            # without re-walking the id. trace_id is "<bucket>/<filename>" —
            # filename never contains '/', so the bucket is everything before
            # the last slash.
            if isinstance(data, dict):
                last = trace_id.rfind('/')
                data.setdefault('bucket', trace_id[:last] if last >= 0 else '(root)')
            self.send_json(data)
        except json.JSONDecodeError:
            self.send_error(400, 'Invalid JSON in trace file')

    def send_json(self, data):
        body = json.dumps(data).encode('utf-8')
        self.send_response(200)
        self.send_header('Content-Type', 'application/json')
        self.send_header('Content-Length', str(len(body)))
        self.send_header('Access-Control-Allow-Origin', '*')
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, format, *args):
        pass  # quiet


print(f'Builder trace viewer: http://localhost:{PORT}/system/builder/web/index.html')
print(f'API:                  http://localhost:{PORT}/api/traces')
print(f'Scanning roots:')
for r in ROOTS:
    print(f'  - {r}')
# Surface the current bucket set so the operator can sanity-check at startup.
buckets = sorted({t['bucket'] for t in _list_all_traces()})
if buckets:
    print(f'Found traces in {len(buckets)} bucket(s): {", ".join(buckets)}')
else:
    print('No traces found in any root.')
class ThreadingHTTPServer(socketserver.ThreadingMixIn, http.server.HTTPServer):
    # Daemon threads so Ctrl-C cleanly tears down in-flight requests.
    daemon_threads = True


ThreadingHTTPServer(('', PORT), Handler).serve_forever()
