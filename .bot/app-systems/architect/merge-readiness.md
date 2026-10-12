# app-systems → runtime2: what's left (2026-10-12)

## Why

Ingi (2026-10-12): "I would like to merge all the way back to runtime2 … only coder has been doing the work, and we havent been going throught the right process, e.g. coder -> tester -> security -> audit … We need to make sure all the plans are checked off … is there anything critical left that we should fix before we merge into parent branch."

## Where we are

- **The line:** runtime2 (`0ea5a4b94`, 2026-06-05) → scalars-as-native → ireader-read-path → settings-config-unification → cli-app-property-override → module-discovery → get-builder-running → builder-formal → app-systems. Eight branches, 4,056 commits. runtime2 and every parent have nothing of their own since, so each step up is a fast-forward.
- **Reviews:** no branch on the line since runtime2 had a tester, security or auditor pass. codeanalyzer last ran on scalars-as-native (06-07). The last full chain was collections-are-data (06-05, merged into runtime2).
- **Merged today:** plang-os-stable (`8d28d8f1d`, 105 commits, +18k lines; the surface re-pinned with Ingi's yes). Gate 158: the baseline, plus 16 terminal permission tests that fail on WSL (Landlock off; production fails closed).

## Critical before merging up

1. **The plang test tree is mostly unbuilt.** `plang --test` from `test/` (before the os merge): 347 tests, 65 pass, 1 fails (`module/math/DoublePlusDecimal_Errors`: the double + decimal no longer errors), **281 have no `.pr`** since the move to `test/`. The builder bot's batches rebuild it (batch 1, `module/math`, is done); each red then gets an owner.
2. **Security review of the whole line.** It hasn't run since 06-05. Known open: **312**, an action's parameters on the wire written as authored in every view: the Store face was fixed (394), and the other views were never swept or tested. To review: permissions and `Authorize` (the os-folder ask, trust by origin, Clean/Keep, the terminal's Landlock sandbox), `[Sensitive]` masking and the secret type, signing (sign-if-missing on the wire). Rotation of the six keys that leaked into transcripts was skipped by Ingi (5.1).
3. **The 17 C# baseline failures, each with a disposition:**
   - **0d, 3 tests** (`Trace_HandleReportsConceptName_NotNamespaceTail`, `Variables_HandleReportsConceptName_NotNamespaceTail`, `CallStack_ReportsItemApex_KindCallstack`): 0d is settled and built (342, 500: `%!variables%` gone, `%!callStack%` and `%!trace%` their own types), so these tests likely assert the old shape. Rewrite or delete.
   - **306, json when needed, 5 tests** (`Materialize_JsonArrayRoot_NarrowsToListValueType`, `Materialize_JsonObjectRoot_NarrowsToDict`, `Navigation_ReadsValueWhichMaterialises`, `Roundtrip_PreservesData`, `Roundtrip_StreamBased_PreservesData`): 401 says "stays text … navigation is the need", but the tests were never closed against it. Ingi's word, then fix or delete.
   - **The snapshot family, 6 tests** (`ThrowTimeSnapshot_EditSurvivesResume`, `MidStackChain_SurvivesDisk_ResumesDeep_AndUnwindsToEntryGoal`, `NavigateAndEditCapturedVariable_ThenResumeToSuccess`, `PlangPath_AsSnapshotConvert_EditSurvivesResume`, `SerializedString_ConvertsToSnapshotViaTypeSystem_AndResumesToSuccess`, `TypedSnapshotString_NavigateEditResume_PersistsEdit`): the redesign is settled (527, plan `test/plan/snapshot/`, stages 0–4) and not built.
   - **typedreturn, 2 tests** (`BuilderValidate_BuildReturnsOkWithTypeName_SetsTerminalVariableSetType`, `BuilderValidate_OnlyOneTerminalVariableSetPerStep_LastInChainWins`): an older plan, moved under `test/plan/typedreturn/` in the reorg, with 12 unbuilt plang tests. Close it or delete it.
   - **1 test** (`EveryInstanceField_IncludingInherited_IsReadonly`): needs an owner.
4. **The terminal's 16 permission tests skip without Landlock**, or every gate on WSL is red (routed to the coder, test-only).

## Plans

- **app-systems:** stages 1–12 done. Open: 10b (C), a typed list's element births, eager or lazy (Ingi). The five plan tests under "validated by" are built when Ingi says.
- **task:** built; its plan tests pass in the suite.
- **list-query:** `list.query` is built; its plang tests under `module/list` are among the unbuilt.
- **snapshot:** a plan only, not built; it carries 6 of the 17.
- **typedreturn:** an older plan; see 3.
- **The parents' plans:** `.bot/{scalars-as-native, settings-config-unification, cli-app-property-override, module-discovery, builder-formal}/architect/plan.md` and `get-builder-running/architect/summary.md`. Nobody has checked them off; that's the auditor's pass.

## Open, not critical (close or carry with a decision)

- **Waiting on Ingi from earlier:**
  - 322: a value's history and events;
  - 313: partial writes;
  - 310: only the app's module carries `.list`;
  - 292 (d): signed Data in json;
  - 0b: the LLM trace belongs to llm;
  - binding;
  - 222: keys;
  - channel `Timeout`;
  - 226: a missing `%!app…%` node at run;
  - 228: `environment.start`;
  - 229: the timer as a value;
  - return depth;
  - 236: the `as <type>` one-liner.
- **Today's:** 63 and the page runtime; 5.2–5.4; 4fb4b4199 (keep or revert).
- **Carried in with the os merge** (the os audit's §5, core bends):
  - `click` is still in the core event set (660's window events approved, not built; `screen.open` keeps `OnWindow`);
  - reflection is exact-case-first, so that `app.parent` and `app.Parent` can coexist (one name, two things);
  - `goal.Enter` forks on `Elsewhere`;
  - `path.file.Program`;
  - the format names: `call.list.SetFormat` (verb+noun), `type.root.FormatOf`, and root's `Get`/`Set` forking on the string `"format"`.
  - Also: the video pass-through is experimental, and two os bugs are open (ShellUpdate's `%file!relative%` comes back empty; the two-video freeze).
- **Builder issues still open:**
  - 3, 5, 6, 7, 8, 20, 22, 23, 26 (1), 35, 41, 43a;
  - 34's measurement (unblocked now that terminal is in);
  - 38 (Ingi).
- **Children:**
  - app-systems-property (4 commits; 7.2 archived, 7.1 rows next);
  - docs/screen-notes and educator-lesson (docs off an older app-systems);
  - plang-os and plang-os-redirect (09-30, likely superseded by plang-os-stable).

## The process back (proposal)

1. Freeze features on app-systems: fixes for the lists above only.
2. The builder bot rebuilds the test tree; the coder fixes the reds.
3. **codeanalyzer:** an OBP scan of the locked core's diff `runtime2..app-systems` (the core is where it matters; the whole diff is 4k commits).
4. **tester:** the plang suite and the C# suites, every plan's tests run, the 17 dispositioned.
5. **security:** the areas in Critical 2.
6. **auditor:** every plan on the line checked off or carried, each with its decision.
7. **docs:** merges up; each parent is a fast-forward, and runtime2 takes app-systems' tip.
