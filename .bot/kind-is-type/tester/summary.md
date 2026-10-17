# kind-is-type — tester summary

| version | HEAD | verdict | what |
|---|---|---|---|
| v1 | 2e83d21ff | FAIL (tests) | No new reds. 10 mutations, 8 caught. F1: file.read's Type at run is a false green (pinned now by the added plang AsTextLandsTextUncaptured; the C# assert still to fix). F2: the builder's kind follow-up has no deterministic test (Refine as a no-op passes everything). OPEN: the plan's stage-3 proof (SetAsImageGifStrictMismatch) is red in the baseline. |

Next (v2): re-run after F2's test and F1's C# assert land; each must go red under its mutation (M8, M5a).
