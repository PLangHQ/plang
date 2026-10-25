# fast-apps — the tester's verdict on the branch's tests (decision 87)

Tip `8c8ab5830` (code `d050b9f50` and after). Compared to the branch's start, `013396728` (stage 0).

**Verdict: needs-fixes — two things, neither a red test.** No new red names; the plan's tests exist and pass, bar the four
names the index gets wrong; one test is a false green for what the plan says it proves.

## 1. No new red names against stage 0

- C# sweep (`./dev.sh test --force`, tip): Modules 1,344 / Types 964 / Data 877 / Generator 191 / Runtime 1,337 green.
  Wire 601 with 6 red: `MidStackChain_SurvivesDisk_ResumesDeep_AndUnwindsToEntryGoal`,
  `NavigateAndEditCapturedVariable_ThenResumeToSuccess`, `PlangPath_AsSnapshotConvert_EditSurvivesResume`,
  `SerializedString_ConvertsToSnapshotViaTypeSystem_AndResumesToSuccess`, `ThrowTimeSnapshot_EditSurvivesResume`,
  `TypedSnapshotString_NavigateEditResume_PersistsEdit`: the same six names the kind-is-type baseline carries
  ("Wire snapshot/resume; fails on runtime2 too"). Totals at stage 0: Modules 1,333 / Types 951 / Wire 596 / Data 863 /
  Runtime 1,242: every count is up, none down.
- `plang --test`: 298 pass, 1 fail (`/app/type/item/BareLiteralJudgedByForm.test.goal`), 3 stale, 1 skipped, in each of
  the five-plus runs (stage 5's three and the instrumented runs). Stage 0: 294 pass, the same fail, 3 stale, 1 skipped
  (the 4 more passes are the branch's own plan goals).

## 2. Removed tests have a reason

Eleven test methods went. Nine are named in the diff:

| Removed | By | Why |
|---|---|---|
| `CreateChild_CreatesWithClonedVariables`, `CreateChild_SetsParent`, `CreateChild_AcceptsCustomVariables`, `Clone_CreatesIndependentCopy`, `Clone_IndependentData`, `Clone_AcceptsCustomVariables`, `Clone_PreservesSetup`, `StreamChannel_ReadAllTextAsync_HonorsLatin1Encoding` | `1dd447b73` (decisions 35, 75, 76) | the production they called is gone: only a test called it |
| `Service_Identity_NavigatesToAppSystemIdentity` | `0363b1467` (35, 41) | `service.Identity` went: only a test read it |
| `TestAppHasItsRunnerAsParent` | `6a7e5a517` (18) | the in-process parent is deferred to its first user |

No test file is deleted. I did not trace the other two removed `[Test]`/`[Arguments]` attributes (11 in the diff, 9 named
above plus the parent test): they sit in the same commits.

## 3. The plan's tests (`test/plan/fast-apps/start.goal`)

Each behaviour's test, whether it exists, whether it passes (the sweep above), and how I know it fails without the change.

| Behaviour | Test | Exists / green | Fails without its change |
|---|---|---|---|
| step cache goes with the app | `CacheLifetimeTests` | yes / yes | red commit `ccf82ba90` (test first) |
| a request app leaves nothing behind | `RequestAppsLeaveNothingBehindTests` | yes / yes | red commit `ccf82ba90` |
| child System has caller's identity / stops with caller / own memory | `ChildSystemTests` (3) | yes / yes | **mutation**: origin identity → null: `HasItsCallersIdentity` red; shutdown token unlinked: `StopsWithItsCaller` red (the third, own memory, not mutated). Test and change came in one commit (`854dd1fb2`), so no red commit |
| code facts read once per code | `CodeFactsTests` (2) | yes / yes | same commit as the change (`854dd1fb2`/`090847b61`); not mutated: asserts `HasOwn` false and same reference as the parent, the bench's 0.15 ms child birth is the other guard |
| shared type list, layer, refusal, parent loading after | `SharedTypeListTests` | yes / yes | red commits `14b255c6d`, `75c0c1594` |
| plugin type only in its app (plang) | `PluginTypeJoinsOnlyItsApp`, `SiblingDoesNotSeeThePluginType` | yes (pr, built) / yes | `plang --test` green; not mutated |
| module registry shared, refused | `SharedModuleTests` | yes / yes | red commit `cb3b9e661` |
| hold: binding on its actor's context | `HoldTests`, `HeldBindingTests` | yes / yes | red commits `17a51bd58`, `b916f6064` |
| a run never writes a shared object | `SharedEntryWritesTests` | yes / yes | red commit `e6877b410` (decision 29) |
| goals through the parent | `SharedGoalTests` | yes / yes | red commit `b916f6064` |
| kind lookup takes no lock | `KindLookupTests` | yes / yes | **fails the deletion test**: see finding 1 |
| every app made from something, lists a layer | `OriginTests` | yes / yes | red commit `5d784c502` |
| a birth makes only what's asked | plan names `BirthTests (ABareApp_MakesNothingNotAsked)` | **not by that name**: `BirthTests` holds eleven `…AreMadeWhenFirstAsked` tests, one per thing (tasks, variables, table, calls, nodes, providers, permission view, attachment, kept list) | red commits `5b38cc183` … `ab1ef713a` |
| path's schemes are kinds of path | plan names `PathSchemeTests (TheSchemes_AreKindsOfPath)` | **not by that name**: nearest is `FamilyKindTests.QuerysClausesAreItsKinds_PathsSchemesAreNotHeldTwice` | no red commit seen |
| list positional read takes no lock | plan names `ListSnapshotReadTests` | **no such test**; `ListCopyOnWriteTests` (a read sees one snapshot until the next write) does not assert a lock | none; no stage-5 commit for it (the stage-5 commits are (1) store, (3) facts) |
| unwritten in-memory store answers empty without opening | plan names `EmptyStoreTests (AnUnwrittenStore_AnswersEmpty_WithoutOpening)` | exists as `InMemoryStoreTests.ABlankStoresRead_OpensNoDatabase` and `TwoAppsWithOneId_ShareNoStore` | red commit `ac80e0240` |
| class facts read once per code | plan names `ClassFactsTests` | exists as `BirthTests.ARequestReadsItsSettingClassesFacts_OncePerCode` | red commit `b68546552` |

Red commits are the coder's own: I read what each adds (one test file, no production change) but did not rebuild each
parent to see it red, except where I mutated (above). A rebuilt parent would not compile for the Stage 2 tests (they
need the actor-taking door, `error CS1503`), which is red in the weak sense only.

## Findings

1. **`KindLookupTests` is a false green for "the kind lookup takes no lock"** (major). I put the pre-`b67c9b782`
   `type/kind/empty/this.cs` (the locked lookup) back: both tests pass (`failed: 0`, reverted, tree clean). They assert
   that a reader never sees a half-made table while 2,000 kinds are added: a locked lookup satisfies that too. The plan
   line is "takes no lock". A test that proves it holds the type's lock on one thread and looks a kind up from another
   with a timeout: a locked lookup blocks, the lock-free one answers.
2. **The plan index names tests that don't exist under those names** (minor): `PathSchemeTests`,
   `TheSchemes_AreKindsOfPath`, `ABareApp_MakesNothingNotAsked`, `ListSnapshotReadTests`, `EmptyStoreTests`,
   `ClassFactsTests`. Three exist under other names (table above); path schemes have a neighbour only; the positional-read
   no-lock behaviour has no test and no commit. `start.goal` is "each behaviour a comment, the step proves it": fix the
   names or say the behaviour was dropped (architect's call).
3. **`CodeFactsTests`, `ChildSystemTests` (own memory) and `PluginTypeJoinsOnlyItsApp`/`SiblingDoesNotSeeThePluginType`
   were not mutation-tested** (note): read as sensitive; say if you want them mutated.

## The flake `ErrorInPlay_DuringRecovery_IsTheTimeoutsOwnVerdict`

Seen once in the stage 1 Modules run (the message was `Expected "Timeo…"`). Not reproduced since: 15 runs alone, 70 runs
at 14-way concurrency (all `failed: 0`), and the Modules suite green in every sweep from stage 2 to the tip (about ten).
The test is older than the branch (`f13566924` and before); the branch's only change to it is `3312602c3` (it makes its
app from the process parent). It races a 1 ms timeout against a 3 s `timer.sleep` and reads the verdict the recovery
sees, so it is timing-sensitive by design. **I can't say it is the branch's, and nothing in the branch's diff touches the
timeout path; the evidence is for older (a race in the test), not proof.**

## The clean-build row (decision 87)

Five quiet clean builds on the tip: **33.8, 33.8, 47.5, 53.8 s** (93.4 once with another session's compiler running
beside it); incremental 9.5–14.9 s (22.7 once). Stage-6-before's 45.9 s sits inside that spread. No quiet run is above
60 s, so there was nothing for the RuntimeIdentifier restore (`26c9e0b9c`) to explain and I made no scratch revert.
The 65–98 s clean builds in the review rows were taken at load 10–21. Row in `Tools/Bench/record.md`.
