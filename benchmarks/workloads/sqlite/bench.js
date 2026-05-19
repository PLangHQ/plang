// sqlite: SELECT one row from an in-memory SQLite DB per iteration.
// Requires `better-sqlite3` to be installed.  Run:
//   cd benchmarks/workloads/sqlite && npm i better-sqlite3
//
// If the package is missing the script exits with a clear message rather
// than a stack trace, so the harness records a clean skip.
let Database;
try {
  Database = require("better-sqlite3");
} catch (e) {
  process.stderr.write(
    "better-sqlite3 not installed — `cd benchmarks/workloads/sqlite && npm i better-sqlite3`\n"
  );
  process.exit(2);
}

const N = parseInt(process.env.BENCH_N || "10000", 10);
const db = new Database(":memory:");
db.exec(`
  CREATE TABLE kv (id INTEGER PRIMARY KEY, name TEXT, value INTEGER);
  INSERT INTO kv (name, value) VALUES ('plang', 42), ('node', 99), ('sqlite', 7);
`);
const stmt = db.prepare("SELECT name, value FROM kv WHERE id = ?");

const durations = new BigInt64Array(N);
let sink = 0;
const loopStart = process.hrtime.bigint();
for (let i = 0; i < N; i++) {
  const t0 = process.hrtime.bigint();
  const row = stmt.get(((i % 3) + 1));
  sink += row.value;                         // anti-DCE
  durations[i] = process.hrtime.bigint() - t0;
}
const loopNs = process.hrtime.bigint() - loopStart;
if (sink < 0) console.log(sink);
process.stdout.write("LOOP_NS=" + loopNs.toString() + "\n");

const out = [];
for (let i = 0; i < N; i++) out.push(durations[i].toString());
process.stdout.write("DURATIONS_NS=" + out.join(",") + "\n");
