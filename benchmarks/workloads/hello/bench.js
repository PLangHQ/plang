// hello / noop: assign a string literal in a tight loop.
const N = parseInt(process.env.BENCH_N || "10000", 10);
const durations = new BigInt64Array(N);

const loopStart = process.hrtime.bigint();
for (let i = 0; i < N; i++) {
  const t0 = process.hrtime.bigint();
  const x = "hello";
  if (x.length < 0) console.log(x);          // anti-DCE guard, never fires
  durations[i] = process.hrtime.bigint() - t0;
}
const loopNs = process.hrtime.bigint() - loopStart;
process.stdout.write("LOOP_NS=" + loopNs.toString() + "\n");

const out = [];
for (let i = 0; i < N; i++) out.push(durations[i].toString());
process.stdout.write("DURATIONS_NS=" + out.join(",") + "\n");
