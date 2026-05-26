#!/usr/bin/env bash
# Restart the branches overview server. Run from anywhere.
set -e
cd "$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"
pkill -f "python3 .bot/branches/server.py" 2>/dev/null || true
sleep 1
nohup python3 .bot/branches/server.py > /tmp/branches-server.log 2>&1 &
disown
sleep 2
curl -s -o /dev/null -w "branches server: HTTP %{http_code} (port 8083)\n" http://localhost:8083/
