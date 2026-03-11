# Auditor Summary -- runtime2-action-conditions

**v1**: Final audit of coder v1. OBP compliance clean. Two major findings: If.Run() and Compare.Run() can throw unhandled exceptions from the evaluator (NotSupportedException, ArgumentException, OverflowException), violating the "behavior methods never throw" contract. Verdict FAIL — send back to coder. See [v1/summary.md](v1/summary.md).
