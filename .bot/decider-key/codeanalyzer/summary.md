# codeanalyzer summary (decider-key)

**Version:** v1

**What this is:** the TypeSafe decider's key asks once (secretly, saved) or names both ways to give one, instead of a 403; one interface, `IKey`, serves the llm and decider settings.

**What was done:** v1 FAIL, one small finding: the ask's error carried as the cause of Missing is unpinned (mutation: all tests green). Rest clean; bench in band (ptest 4.5 s, sweep 40.6 s, red only Wire).

**Next:** coder adds the cause assertion; re-check, then tester.

**Code example:**
```csharp
if (asked.Error is { } error) missing.list.Add(error);   // IKey.Asked: the cause, whole
```
