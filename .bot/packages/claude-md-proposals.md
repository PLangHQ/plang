## architect — packages 3 — 2026-10-25
**Target:** `CLAUDE.md`, the "Owners take values; handlers open carriers" bullet (Runtime2 Conventions)
**Why:** the bullet came from my decision 231 (app-systems, 2026-09-28), which Ingi never ruled. On 2026-10-17 (decision 851) Ingi rejected it: "yes, this is a bad rule. data should flow as far as it can". The replacement was proposed on kind-is-type (`.bot/kind-is-type/claude-md-proposals.md`, architect v2), but the bullet is still in CLAUDE.md:21, so every handler is still taught to open its slots, the opposite of the rule. Found again by plang-docs in the todos.md triage (entry 1562).
**Proposed change:** replace the bullet with:

```
- **Data flows as far as it can.** A carrier (`data<T>`) is passed on whole — to an owner, a value, a constructor — and opened only by whoever needs the value, when it needs it. Don't open a slot to hand its value on, and don't open it because a later owner will need it (Ingi, decision 851). An owner's operation never takes CLR primitives (`string`, `bool`, `int`); the CLR lowering happens only at the real boundary inside the owner (the System.IO call, the .NET API).
```

## architect — packages 14 — 2026-10-25
**Target:** `CLAUDE.md`, the "Speed record" section
**Why:** Ingi asked codeanalyzer to cut the bench's waiting (112f54bf7, 1c8cc3021). `Tools/Bench/run.sh` now rests 0 s by default, takes the parts a diff touches, and spends nothing on OpenAI. The two 300 s rests run only for a diff that touches the store, its pool or what a request app holds. CLAUDE.md still says the bench measures everything, held memory "after 3,000 requests and a rest", at every review.
**Proposed change:** replace the section's two bullets with:

```
- `Tools/Bench/run.sh` measures, gated on the machine's load: a sub app's and a root app's birth (time, bytes), a request app (time, bytes) with its held memory after 3,000 requests, `plang --test`'s time, the C# sweep's time, and the C# build (clean and incremental). Each run appends a row to `Tools/Bench/record.md` (date, branch, commit, load, each number); each number's noise band is at the record's top. A whole run is about 5 minutes and calls no OpenAI (no key, the llm tag left out).
- Codeanalyzer runs it at every review, **the parts the diff touches** (run.sh's header says which: a package-only diff skips birth and request), and compares with the last row recorded on the branch's parent. The two 300 s rests (`BENCH_REST=300`) only for a diff that touches the store, its pool, or what a request app holds. A number outside its band is a finding: blocking unless the branch's decisions explain it. Held memory that grows with the request count always blocks.
```
