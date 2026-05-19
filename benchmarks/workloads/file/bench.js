// file: read a small fixture file on every iteration (sync, like plang's read).
const fs = require("node:fs");
const path = require("node:path");

const N = parseInt(process.env.BENCH_N || "10000", 10);
const FIX = path.join(__dirname, "fixture.txt");
const durations = new BigInt64Array(N);

let sink = 0;
const loopStart = process.hrtime.bigint();
for (let i = 0; i < N; i++) {
  const t0 = process.hrtime.bigint();
  const buf = fs.readFileSync(FIX, "utf8");
  sink += buf.length;                        // anti-DCE
  durations[i] = process.hrtime.bigint() - t0;
}
const loopNs = process.hrtime.bigint() - loopStart;
if (sink < 0) console.log(sink);
process.stdout.write("LOOP_NS=" + loopNs.toString() + "\n");

const out = [];
for (let i = 0; i < N; i++) out.push(durations[i].toString());
process.stdout.write("DURATIONS_NS=" + out.join(",") + "\n");
