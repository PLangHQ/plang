# codeanalyzer summary (decider-key)

**Version:** v2

**What this is:** the TypeSafe decider's key asks once (secretly, saved) or names both ways to give one, instead of a 403; one interface, `IKey`, serves the llm and decider settings.

**What was done:** v1 FAIL: the ask's error carried as the cause of Missing was unpinned. v2 PASS: both no-one-to-ask tests assert it (`ChannelEof`), red when the line is dropped. Bench in band (ptest 4.5 s, sweep 40.6 s, red only Wire).

**Next:** tester.

**Code example:**
```csharp
if (asked.Error is { } error) missing.list.Add(error);   // IKey.Asked: the cause, whole
```
