# doc2 v1 summary

**What this is:** the first slice of `doc/`, a documentation folder for a stranger: what PLang is, install, a first goal (read a JSON file, print a field), and what a failed build looks like.

**What was done:** `doc/start.md`, `install.md`, `first-goal.md`, `when-a-step-fails.md`, `verify.md`. 32 claims recorded. The run half is verified on built goals, with no key. The build half is verified only as far as failure: no decider key, so a successful build and step-level failures are unverified.

**Findings that differ from the old docs:** `plang exec` is no command; `--version` is refused; run never builds; the latest release is v0.1.17.3 (Feb 2025); `.pr` files are `.build/<goal>.pr`; a first build asks to create the app (`--app.setting.create=true` skips it); the build's key is `TYPESAFE_API_KEY` (setting `llm.decider.setting.key`), not `OPENAI_API_KEY`.

**Next:** a cold read of `doc/`; get a decider key to verify the unverified rows.
