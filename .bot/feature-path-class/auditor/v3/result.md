# Auditor v3 — Finding-by-Finding Gap Analysis

## How to read this

For each tester finding, I document: what the tester caught, what I knew at the time, whether I should have caught it, and why I didn't.

---

### Tester Finding #1 (Critical): All 6 try/catch blocks had zero test coverage

**What the tester caught**: The coder added exception handling (try/catch) to all 6 behavior methods in v6. All 1227 tests passed. But no test actually triggered those catch blocks — the exception handling code existed but was never exercised. Classic false-green.

**What I knew at auditor v2**:
- I had *requested* this exact code in v1 finding #1 (critical)
- I read the code in v2 and verified the try/catch pattern was correct
- In my v1 report.json test review action, I literally wrote: *"Missing: exception paths (locked files, permission denied)"*
- I had the test file open — I reviewed PathTests.cs

**Should I have caught it?** Yes. Unambiguously.

**Why I missed it**: I verified the *code* was added but didn't verify *tests* were added for that code. My v1 action explicitly noted "Missing: exception paths" in the test review, but when the coder submitted v6, I only checked the production code side. I treated "code fix verified" as "finding resolved" without checking the test side. My v2 review table says "#1 — Correct. Consistent pattern." That was about the code pattern, not test coverage.

**Severity of my miss**: This is the worst one. I'm the one who flagged the missing exception handling. I knew the catch blocks were new code. I knew the tests didn't cover exception paths (I wrote it). And I still approved without checking.

---

### Tester Finding #3 (Resolved): Copy/Move overwrite conflict tests missing

**What the tester caught**: Copy and Move with `Overwrite=false` when destination already exists — no test verified that an IOException error is returned.

**What I knew at auditor v2**:
- I flagged Move.Overwrite as major finding #3 in v1
- The coder fixed it in v6 (delete existing dir before move)
- In v2, I noted "ResolveDestination not applied to Move" as a new observation
- I verified the overwrite *code* existed

**Should I have caught it?** Yes. Same pattern as #1 — verified the code path existed but not that tests exercised the conflict scenario.

**Why I missed it**: My focus was on whether the *feature* (overwrite support) worked correctly. I checked that the coder's implementation was sound. I didn't check that a test actually creates an existing destination and verifies the conflict error when overwrite=false.

---

### Tester Finding #4 (Resolved): Save serialization path untested

**What the tester caught**: Save has 3 branches: string content, byte[] content, object (serialized via SerializeAsync). The object branch had no test.

**What I knew at auditor v2**: I reviewed Save's code. The 3 branches were visible. I didn't flag the missing test.

**Should I have caught it?** Yes, but this one is less obvious. My v1 review focused on safety (exception handling, prefix bugs, etc.), not branch coverage. A code auditor could reasonably focus on "is the code correct?" and leave "is every branch tested?" to a tester. But I should have noticed that a significant code path (JSON serialization) had no test.

**Why I missed it**: I was thinking about code correctness, not code coverage. The Save method looked right — all three branches are straightforward. But "code looks right" and "code is tested" are different things.

---

### Tester Findings #5-#7 (Resolved): Weak assertions

**What the tester caught**:
- Error tests only checked `Success == false`, not `Error.Key` or `Error.StatusCode`
- Relative test used `Contains` instead of exact string match
- List test only checked count, not actual file names

**What I knew at auditor v2**: I read every test. I reviewed the Relative fix and wrote "Tested." I reviewed the List tests when checking Pattern defaults.

**Should I have caught it?** Partially. My character description says "Do tests verify intent or just implementation?" — weak assertions are exactly this. But assertion strength is more of a testing concern than an auditing concern. I think catching the Relative `Contains` is reasonable (I was specifically looking at that test for my finding #2 fix), but the error assertion pattern and List count-only are more in the tester's domain.

**Why I missed it**: I was checking "does a test exist for this scenario?" not "is the test's assertion strong enough to catch a regression?" Two fundamentally different questions. I answered the first and didn't ask the second.

---

### Tester Finding #8 (Resolved): Copy doesn't verify source still exists

**What the tester caught**: After Copy, the test verified the destination exists but didn't verify the source wasn't accidentally deleted (which would make it a Move, not a Copy).

**Should I have caught it?** This is a quality-of-testing concern. It's a valid point but it's the kind of thing that distinguishes a good test from a great test. I wouldn't expect an auditor to catch this routinely, but it's worth knowing about.

---

### Tester Finding #2 (Minor): No PLang .goal tests

**What the tester caught**: No PLang goal tests for file operations.

**What I knew**: I noted this too (it's in CLAUDE.md as a requirement). The coder acknowledged it's blocked on the builder. Both the tester and I agree: not blocking merge.

**Should I have caught it?** I did catch it — it was a known limitation.

---

## Summary: What I Should Add to My Review Process

| Gap | Fix |
|-----|-----|
| Verified code exists without verifying tests exist | When a fix adds new code paths, require corresponding new tests |
| Treated "code fix verified" as "finding resolved" | A finding is only resolved when BOTH code AND tests are verified |
| Didn't run code coverage | Run coverage on changed files before approving |
| Checked test existence, not assertion quality | Read assertions specifically: what value is checked? Is it specific enough to catch a regression? |
| Focused on production code correctness | Allocate equal review time to test code quality |
