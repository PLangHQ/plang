# Coder handoff: `test.report` crashes on JSON cycle in failure variables

**From:** builder
**Branch:** `fix-stepvartypes-incremental`
**Severity:** medium — blocks `Tests/.test/results.json` being written **at all** whenever any test in the run fails with an AssertionError carrying a Variables snapshot. Pre-existing; biting now because the webui depends on results.json being current.

## Repro

```bash
cd /workspace/plang/Tests
/workspace/plang/PlangConsole/bin/Debug/net10.0/plang --test --include=Modules/Math
```

(Any subset that includes at least one failing test reproduces.) Output ends with:

```
test.report: JsonException: A possible object cycle was detected. ...
Path: $.runs.variables.Error.CallFrames.Caller.Chain.Chain.Chain.Chain.Chain.Chain. ... (repeats ~28 times) ... .Id.
```

`Tests/.test/results.json` is NOT written. The file on disk is whichever run last completed successfully — so the entire `--test` invocation produces zero updated output.

## Diagnosis

`PLang/app/modules/test/report.cs:BuildJson` serializes each run's `(run.Error as AssertionError)?.Variables` field directly. The Variables snapshot is the runtime variable map captured at the moment of assertion failure — and at least one snapshotted variable transitively references the `App.CallStack` tree (`Error` → `CallFrames` → `Caller` → `Chain` cycles back).

System.Text.Json with default options aborts on cycle.

## What we want

The whole serialization NOT to fall over. The variables snapshot is useful for debugging, but the Error/CallFrames cycle inside it is noise — we don't need the call tree serialised under each variable.

Two options, both reasonable:

**A. Configure the JsonSerializerOptions** to handle cycles. `ReferenceHandler.IgnoreCycles` is the simplest fix — drops the cyclical reference on second visit, no `$id`/`$ref` noise added. One-line change at the `Format.Options` site or local override in `BuildJson`.

**B. Sanitize the variables map before serialize**: walk the dictionary, replace any `IError` / `CallFrame` / `Call` instance with a short string like `"<error: ...>"` or just drop those keys. Slightly more code, but produces cleaner JSON output (no truncated nested objects).

Either is fine. **A** is cheaper if you're not worried about JSON noise. **B** is what we'd want long-term but isn't urgent.

## Verification

After the fix, on a known-failing test set:

```bash
cd /workspace/plang/Tests
/workspace/plang/PlangConsole/bin/Debug/net10.0/plang --test --include=Modules/Math
python3 -c "import json; d=json.load(open('.test/results.json')); print(d['summary'])"
```

Should print the summary dict (proves the file got written), not throw. Each Fail entry in `runs[]` should still carry an `error` message + the new `output` and `timings` fields shipped in 4a28174e5.

## Out of scope

- Don't touch the new `output` / `timings` fields — those work.
- Don't strip Variables entirely — its non-cyclical content (regular variable values) is genuinely useful for failure debugging. Just kill the cycle.
- Don't change the console rendering — only the JSON path is broken.
