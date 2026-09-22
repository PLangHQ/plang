# Wire's 24 reds, classified — read-only map, nothing fixed

Branch `goal-graph-singular`, at `947c973c4`. Wire is 470/24 (baseline when the suite first reported
was 29; five went green with the Providers value types). This is the map the next session starts
from. **Nothing here was fixed.**

> **CORRECTION — the sentence below was wrong, and the error is instructive.** I originally wrote
> that none of the 24 is a branch regression, on the grounds that all 24 were in the baseline. That
> baseline was captured **on this branch**, so it could only ever show what the snapshot work
> changed — never what the branch as a whole broke against `runtime2`. Provenance run against
> `origin/runtime2` in a worktree: **`Cut4_TamperingPropertyValue_FailsOuterSignatureVerify`,
> `OuterSignature_AfterPropertiesValueTamper_FailsVerify` and
> `AssertionError_Message_MasksSensitiveViaDiagnosticOutput` all PASS on the base.** They are
> **branch regressions**, they are security-shaped, and they fail OPEN. See the Provenance section.
>
> A baseline is only a baseline against the thing you are comparing to.

All 24 were already failing in the branch-local baseline captured before the snapshot work, so
nothing here came from the *snapshot* pass. That is a narrower claim than "not a regression" — see
the correction above. Each is placed by CAUSE below.

## (a) Deferred read half / restore remainder — todos #3

These reach the snapshot fine now and stop in restore-side machinery that was never rewritten.

| Test | Stops at |
|---|---|
| `SerializedString_ConvertsToSnapshotViaTypeSystem_AndResumesToSuccess` | resume path, null |
| `MidStackChain_SurvivesDisk_ResumesDeep_AndUnwindsToEntryGoal` | NullReference |
| `ThrowTimeSnapshot_EditSurvivesResume` | null |
| `PlangPath_AsSnapshotConvert_EditSurvivesResume` | assertion false |
| `TypedSnapshotString_NavigateEditResume_PersistsEdit` | NullReference |
| `NavigateAndEditCapturedVariable_ThenResumeToSuccess` | `InvalidCastException: app.type.item.null.this → IConvertible` |
| `App_Snapshot_WalksISnapshottedProperties_AndAggregatesIntoTree` | assertion false |
| `Testing_RoundTrip_PreservesIsEnabled` | assertion false — this is **presence-as-signal**: `build`/`test` capture nothing and restore on the section merely existing |

`Resume`, the `App.Restore` dispatch ladder and presence-as-signal are exactly the structural
problems recorded in todos #3. They are one pass, not eight fixes.

## (b) The CLR-boundary descendants

| Test | Why |
|---|---|
| `Statics_RoundTrip_PreservesNameValuePairs` | Statics' OWN storage is an untyped `Dictionary<string, Dictionary<string, object?>>` — the same disease one level down. What Statics puts back on restore is Statics' finding, explicitly scoped out. |

The other CLR boundary (Registration / DefaultOverride) is **gone** as of `947c973c4` — those two are
values with their own readers now, and `Rows<T>` reads through the typed ask.

## (c) Flaky — drifts on identical code

`PathKind_FileScheme_ViaCreate_IsFile` and `PathKind_HttpsScheme_ViaCreate_IsHttps`
(`PathSerializerMigrationTests`) measured 0 / 2 / 0 failures across three identical runs. They are
**absent from this particular run's 24** and appear in others, which is what flaky means. Any count
comparison that includes them is noise; diff by NAME.

## (d) Neither — real, and not about the snapshot at all

These are genuine defects sitting in the baseline. Two are worth naming.

**`Deserialize_ShallowNesting_StillWorks` — the `Data<T>` double-wrap footgun, confirmed.** It fails
with an infinitely self-nested value:

```
invalid .pr schema: value slot 'a' has no declared type. Value was:
{"name":"a","value":{"name":"a","value":{"name":"a","value":{"name":"a",…
```

The architect predicted exactly this: *"if the nested-Data symptom SURVIVES the change, it is no
longer the serializer — it is the `data.@this<T>` implicit double-wrap footgun at the CAPTURE
site."* It survived. So this is now visible where it happens, and it is the next real bug rather
than a snapshot artefact. First place to look per that prediction: `Clone()` at
`variable/list/this.Snapshot.cs`.

**`SignatureType_XmlDoc_NoLongerMentionsEnvelope` — a stale test, not a defect.** It reads
`PLang/app/type/signature/this.cs`, which does not exist; signature lives at
`PLang/app/type/item/signature/`. The test asserts a source file's XML doc no longer says
"Envelope", so it is guarding the data-is-not-enveloped rule against a path that moved. One-line
fix, but it is a test bug and should be corrected as one.

The rest, ungrouped, all pre-dating this branch:

- `OnAsk_OnMessageChannel_FiresPreSerialise` — NullReference in a channel event test
- `Roundtrip_PreservesData`, `Roundtrip_StreamBased_PreservesData` — NullReference
- `Serialize_Object_ReturnsJson`, `Serialize_Object_IgnoresNullProperties`, `Serialize_Enum_UsesCamelCase`
- `Properties_RoundTrip_ListOfPrimitives`, `Properties_RoundTrip_NestedDictOfPrimitives`
- `Cut4_TamperingPropertyValue_FailsOuterSignatureVerify`, `OuterSignature_AfterPropertiesValueTamper_FailsVerify` — both "expected failure but Data succeeded", i.e. tamper detection not firing
- `GoalCall_Name_Parallel_Parameters_PrPath_HaveOut`
- `TemplateParam_ResolvesOnLoad_NoFlagStaysLiteral`
- `AssertionError_Message_MasksSensitiveViaDiagnosticOutput` — a masking test whose secret is leaking into the message

The two tamper tests and the masking test are the ones I would look at first after the double-wrap:
each is a security-shaped assertion currently not holding.

## Provenance — three of (d) are BRANCH REGRESSIONS

Run in a worktree at `origin/runtime2` (`0ea5a4b94`), built clean, tests run by name:

| Test | runtime2 | this branch |
|---|---|---|
| `Cut4_TamperingPropertyValue_FailsOuterSignatureVerify` | **PASS** | FAIL |
| `OuterSignature_AfterPropertiesValueTamper_FailsVerify` | **PASS** | FAIL |
| `AssertionError_Message_MasksSensitiveViaDiagnosticOutput` | **PASS** | FAIL |

All three exist on the base and pass there. So this branch broke them, and what they assert is:

- tamper detection on a signed payload — both fail as *"expected failure but Data succeeded"*, i.e.
  a **tampered value now verifies**. Failing open.
- secret masking — the plaintext secret now reaches the assertion message.

This is the top item, ahead of everything else in the order below. It is not snapshot-adjacent and
it should not go to a merge.

Suspects to check first, in order (the architect's, and they match the shape):

1. The `Verify: false` applied on nested/ingest reads (the kind bridge, the data reader's nested
   arms). If a path that used to be the OUTER read is now treated as nested, the outer verify is
   skipped — which is exactly "tampered value verifies".
2. For the masking test, the error-render changes made around `app.Error`'s deletion.

Not started; provenance only, as ruled.

## Order this suggests

0. **The three branch regressions above** — tamper detection failing open and a secret leaking.
   Everything else waits.
1. The `Data<T>` double-wrap (d) — a real bug, now locatable, and it is upstream of value fidelity
   everywhere, not just snapshots. Note: the "look at `Clone()` at the capture site" pointer does
   NOT apply — this is a *deserialize* test, so the place to look is where a Data is assigned into a
   `Data<object>` slot on the READ path (the implicit operator double-wraps when `T = object`).
2. The flaky pass (c) — fix or quarantine with the cause named; never left drifting.
3. The restore/todos-#3 pass (a) — one structural pass, eight tests.
4. (b) Statics' own bag, with (a) or after it.
5. The stale signature-path test — trivial, take it with anything.
