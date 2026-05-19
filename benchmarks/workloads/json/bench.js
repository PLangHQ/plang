// json: serialize a small object on every iteration.
const N = parseInt(process.env.BENCH_N || "10000", 10);
const durations = new BigInt64Array(N);
const payload = { id: 42, name: "plang", ok: true, tags: ["a", "b", "c"] };

let sink = 0;
const loopStart = process.hrtime.bigint();
for (let i = 0; i < N; i++) {
  const t0 = process.hrtime.bigint();
  const s = JSON.stringify(payload);
  sink += s.length;                          // anti-DCE
  durations[i] = process.hrtime.bigint() - t0;
}
const loopNs = process.hrtime.bigint() - loopStart;
if (sink < 0) console.log(sink);
process.stdout.write("LOOP_NS=" + loopNs.toString() + "\n");

const out = [];
for (let i = 0; i < N; i++) out.push(durations[i].toString());
process.stdout.write("DURATIONS_NS=" + out.join(",") + "\n");
