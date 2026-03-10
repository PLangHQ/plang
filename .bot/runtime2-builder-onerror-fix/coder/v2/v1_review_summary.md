# v1 Review Summary

The codeanalyzer (v1) and user identified that v1's approach of manually editing .pr files was wrong:

1. **Manual .pr edits are never allowed** - .pr files are LLM builder output only. The v1 coder manually replaced `retryOverSeconds` with `retryOverMs` in system/builder/.build/ files, violating a fundamental PLang rule.
2. **Test .build folders were deleted** - v1 deleted .build folders but couldn't rebuild because there was no API key available at the time.
3. **Fix: rebuild with the plang builder** - The correct approach is to revert the manual edits and use `plang p build --llmservice=openai` to properly rebuild all .pr files.
