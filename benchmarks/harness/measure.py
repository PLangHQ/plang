#!/usr/bin/env python3
"""
Single-shot measurement of a benchmark command.

Spawns `cmd` as a child process, captures:
  - wall: end - start (seconds)
  - cpu_user, cpu_sys: ru_utime, ru_stime from RUSAGE_CHILDREN delta
  - rss_max_mib: ru_maxrss delta (KiB on Linux, bytes on macOS)
  - per_iter_ns: parsed from stdout — looks for a line `DURATIONS_NS=n1,n2,n3,...`

Emits a JSON dict to stdout. Intended to be called from run.py, not directly.
"""
import argparse, json, resource, subprocess, sys, time, platform, os

def parse_durations(stdout: str) -> list[int]:
    for line in stdout.splitlines():
        if line.startswith("DURATIONS_NS="):
            payload = line[len("DURATIONS_NS="):].strip()
            if not payload:
                return []
            return [int(x) for x in payload.split(",") if x]
    return []

def parse_loop_ns(stdout: str) -> int | None:
    for line in stdout.splitlines():
        if line.startswith("LOOP_NS="):
            payload = line[len("LOOP_NS="):].strip()
            try:
                return int(float(payload))
            except ValueError:
                return None
    return None

def percentile(sorted_vals, p):
    if not sorted_vals:
        return None
    k = (len(sorted_vals) - 1) * p
    f = int(k)
    c = min(f + 1, len(sorted_vals) - 1)
    if f == c:
        return sorted_vals[f]
    return sorted_vals[f] + (sorted_vals[c] - sorted_vals[f]) * (k - f)

def measure(cmd: list[str], cwd: str | None = None, env: dict | None = None) -> dict:
    before = resource.getrusage(resource.RUSAGE_CHILDREN)
    t0 = time.monotonic()
    proc = subprocess.run(
        cmd, cwd=cwd, env=env,
        capture_output=True, text=True,
    )
    wall = time.monotonic() - t0
    after = resource.getrusage(resource.RUSAGE_CHILDREN)

    rss_unit_to_mib = (1.0 / 1024.0) if platform.system() == "Linux" else (1.0 / (1024.0 * 1024.0))
    rss_max_mib = (after.ru_maxrss) * rss_unit_to_mib  # peak across all children — close enough for single-child runs

    cpu_user = after.ru_utime - before.ru_utime
    cpu_sys = after.ru_stime - before.ru_stime

    durations = sorted(parse_durations(proc.stdout))
    n = len(durations)
    loop_ns = parse_loop_ns(proc.stdout)

    return {
        "cmd": cmd,
        "exit": proc.returncode,
        "wall_s": wall,
        "loop_ns": loop_ns,
        "cpu_user_s": cpu_user,
        "cpu_sys_s": cpu_sys,
        "cpu_total_s": cpu_user + cpu_sys,
        "rss_max_mib": rss_max_mib,
        "iter_count": n,
        "p50_ns": percentile(durations, 0.50),
        "p95_ns": percentile(durations, 0.95),
        "p99_ns": percentile(durations, 0.99),
        "stdout_tail": proc.stdout[-2000:],
        "stderr_tail": proc.stderr[-2000:],
    }

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--cwd", default=None)
    ap.add_argument("cmd", nargs=argparse.REMAINDER)
    args = ap.parse_args()
    if not args.cmd:
        print("usage: measure.py [--cwd DIR] -- CMD...", file=sys.stderr)
        sys.exit(2)
    if args.cmd and args.cmd[0] == "--":
        args.cmd = args.cmd[1:]
    result = measure(args.cmd, cwd=args.cwd)
    json.dump(result, sys.stdout, indent=2)

if __name__ == "__main__":
    main()
