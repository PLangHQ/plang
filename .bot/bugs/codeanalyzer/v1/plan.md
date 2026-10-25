# codeanalyzer v1 — bugs (3a29633f8 → ad0caedd7)

Scope: the 56 production files the branch touches, the diff read against the parent, files read whole where the diff
is more than a line (Window.cs, Shown.cs, Bitmap.cs, window/this.cs, view, Saved/Live/IAnswer/exchange, permission,
Executor, start, channel list/base/goal, test/this.cs). One-line touches (http Default, terminal Default, noop/file
channels) read at the diff. OpenAi.cs's older smells are Ingi's exception (bugs 23) and not flagged.

Checks: bench parts the diff touches (birth, request, ptest, sweep; no rest); `plang --test` with the llm tests
included and no key; announced mutations of N15, the named-actor twin and S1; one throwaway probe on Window.Apply.
