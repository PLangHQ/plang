#!/usr/bin/env python3
"""
Orchestrator. For each (workload, runtime) pair: warmup, measure, aggregate, write.

Usage from repo root:
    python3 benchmarks/harness/run.py
    python3 benchmarks/harness/run.py --workload hello
    python3 benchmarks/harness/run.py --n 1000 --repeats 3
"""
import argparse, datetime, json, os, statistics, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BENCH = ROOT / "benchmarks"
sys.path.insert(0, str(BENCH / "harness"))
from measure import measure

PLANG = ROOT / "PlangConsole" / "bin" / "Debug" / "net10.0" / "plang"

# Each workload defines how to invoke plang and node.
# `n_env` is the env var both sides read to set iteration count.
WORKLOADS = {
    "hello": {
        "plang_args": ["--debug={\"timing\":true}"],
        "plang_cwd": BENCH / "workloads" / "hello",
        "node_script": BENCH / "workloads" / "hello" / "bench.js",
    },
    "json": {
        "plang_args": ["--debug={\"timing\":true}"],
        "plang_cwd": BENCH / "workloads" / "json",
        "node_script": BENCH / "workloads" / "json" / "bench.js",
    },
    "file": {
        "plang_args": ["--debug={\"timing\":true}"],
        "plang_cwd": BENCH / "workloads" / "file",
        "node_script": BENCH / "workloads" / "file" / "bench.js",
    },
    "sqlite": {
        "plang_args": None,   # not supported
        "plang_cwd": None,
        "node_script": BENCH / "workloads" / "sqlite" / "bench.js",
    },
}

def runtime_cmd(runtime: str, workload: str, n: int) -> tuple[list[str], Path | None, dict]:
    w = WORKLOADS[workload]
    env = os.environ.copy()
    env["BENCH_N"] = str(n)
    if runtime == "plang":
        if w["plang_args"] is None:
            return None, None, env
        return [str(PLANG)] + list(w["plang_args"]), w["plang_cwd"], env
    elif runtime == "node":
        return ["node", str(w["node_script"])], None, env
    raise ValueError(runtime)

def aggregate(runs: list[dict]) -> dict:
    def vals(k): return [r[k] for r in runs if r.get(k) is not None]
    def med(k): return statistics.median(vals(k)) if vals(k) else None
    return {
        "runs": len(runs),
        "wall_s_median": med("wall_s"),
        "loop_ns_median": med("loop_ns"),
        "cpu_total_s_median": med("cpu_total_s"),
        "rss_max_mib_median": med("rss_max_mib"),
        "p50_ns_median": med("p50_ns"),
        "p95_ns_median": med("p95_ns"),
        "p99_ns_median": med("p99_ns"),
        "iter_count_first": runs[0].get("iter_count") if runs else None,
    }

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--workload", choices=list(WORKLOADS.keys()) + ["all"], default="all")
    ap.add_argument("--runtime", choices=["plang", "node", "both"], default="both")
    ap.add_argument("--n", type=int, default=10000)
    ap.add_argument("--warmup", type=int, default=3)
    ap.add_argument("--repeats", type=int, default=5)
    ap.add_argument("--out", default=None)
    args = ap.parse_args()

    runtimes = ("plang", "node") if args.runtime == "both" else (args.runtime,)
    if "plang" in runtimes and not PLANG.exists():
        print(f"plang binary not found at {PLANG} — build it first, or pass --runtime node.", file=sys.stderr)
        sys.exit(1)

    today = datetime.date.today().isoformat()
    out_dir = Path(args.out) if args.out else BENCH / "results" / today
    out_dir.mkdir(parents=True, exist_ok=True)
    raw_dir = out_dir / "raw"
    raw_dir.mkdir(exist_ok=True)

    workloads = list(WORKLOADS.keys()) if args.workload == "all" else [args.workload]
    summary = {"n": args.n, "warmup": args.warmup, "repeats": args.repeats, "results": {}}

    for w in workloads:
        summary["results"][w] = {}
        for rt in runtimes:
            cmd, cwd, env = runtime_cmd(rt, w, args.n)
            if cmd is None:
                summary["results"][w][rt] = {"skipped": "not supported"}
                continue
            print(f"==> {w} / {rt}: warmup x{args.warmup}", flush=True)
            for i in range(args.warmup):
                measure(cmd, cwd=str(cwd) if cwd else None, env=env)
            print(f"==> {w} / {rt}: measure x{args.repeats}", flush=True)
            runs = []
            for i in range(args.repeats):
                r = measure(cmd, cwd=str(cwd) if cwd else None, env=env)
                runs.append(r)
                (raw_dir / f"{w}_{rt}_{i}.json").write_text(json.dumps(r, indent=2))
                print(f"   run {i}: wall={r['wall_s']:.3f}s cpu={r['cpu_total_s']:.3f}s "
                      f"rss={r['rss_max_mib']:.1f}MiB iters={r['iter_count']} exit={r['exit']}",
                      flush=True)
            summary["results"][w][rt] = aggregate(runs)

    (out_dir / "summary.json").write_text(json.dumps(summary, indent=2))
    write_markdown(out_dir, summary)
    print(f"\nWrote {out_dir}/summary.json + summary.md")

def fmt(v, unit, digits=3):
    if v is None: return "—"
    return f"{v:.{digits}f}{unit}"

def write_markdown(out_dir: Path, summary: dict):
    lines = [f"# Benchmark results — N={summary['n']}, "
             f"warmup={summary['warmup']}, repeats={summary['repeats']} (medians)\n"]
    for w, rts in summary["results"].items():
        lines.append(f"## {w}\n")
        lines.append("| runtime | wall (s) | loop (s) | cpu (s) | RSS (MiB) | wall ops/s | loop ops/s | p50 µs | p95 µs | p99 µs |")
        lines.append("|---------|---------:|---------:|--------:|----------:|-----------:|-----------:|-------:|-------:|-------:|")
        for rt, agg in rts.items():
            if "skipped" in agg:
                lines.append(f"| {rt} | _skipped: {agg['skipped']}_ |||||||||")
                continue
            wall = agg["wall_s_median"]
            loop_s = (agg["loop_ns_median"] / 1e9) if agg["loop_ns_median"] else None
            wall_ops = (summary["n"] / wall) if wall else None
            loop_ops = (summary["n"] / loop_s) if loop_s else None
            def us(ns): return ns / 1000.0 if ns is not None else None
            lines.append(
                f"| {rt} | "
                f"{fmt(wall,'',3)} | "
                f"{fmt(loop_s,'',3)} | "
                f"{fmt(agg['cpu_total_s_median'],'',3)} | "
                f"{fmt(agg['rss_max_mib_median'],'',1)} | "
                f"{int(wall_ops) if wall_ops else '—'} | "
                f"{int(loop_ops) if loop_ops else '—'} | "
                f"{fmt(us(agg['p50_ns_median']),'',2)} | "
                f"{fmt(us(agg['p95_ns_median']),'',2)} | "
                f"{fmt(us(agg['p99_ns_median']),'',2)} |"
            )
        lines.append("")
    (out_dir / "summary.md").write_text("\n".join(lines))

if __name__ == "__main__":
    main()
