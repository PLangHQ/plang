#!/usr/bin/env python3
"""Simple dev server for the builder trace viewer.
Serves static files + /api/traces endpoint that lists trace files."""

import http.server
import json
import os
import sys

PORT = int(sys.argv[1]) if len(sys.argv) > 1 else 8080
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
TRACES_DIR = os.path.join(ROOT, '.build', 'traces')

class Handler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=ROOT, **kwargs)

    def do_GET(self):
        if self.path == '/api/traces':
            self.send_trace_list()
        elif self.path.startswith('/api/traces/'):
            self.send_trace_file(self.path[len('/api/traces/'):])
        else:
            super().do_GET()

    def send_trace_list(self):
        if not os.path.isdir(TRACES_DIR):
            self.send_json([])
            return

        files = sorted([
            f for f in os.listdir(TRACES_DIR)
            if f.endswith('.json') and f != 'manifest.json' and f != 'build-run.json'
        ])
        self.send_json(files)

    def send_trace_file(self, filename):
        path = os.path.join(TRACES_DIR, filename)
        if not os.path.isfile(path) or '..' in filename:
            self.send_error(404)
            return
        try:
            with open(path, encoding='utf-8') as f:
                data = json.load(f)
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
print(f'API: http://localhost:{PORT}/api/traces')
http.server.HTTPServer(('', PORT), Handler).serve_forever()
