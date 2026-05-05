#!/usr/bin/env bash
# Restart the architect review server. Run from anywhere.
set -e
cd "$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"
pkill -f "python3 .bot/architect/server.py" 2>/dev/null || true
sleep 1
nohup python3 .bot/architect/server.py > /tmp/architect-server.log 2>&1 &
disown
sleep 2
curl -s -o /dev/null -w "architect server: HTTP %{http_code} (port 8081)\n" http://localhost:8081/
