# v7 baseline — before any code change (2026-06-10)

Commit: 32dccae13 (clean tree).

## C# (`./dev.sh test`, all 6 slices)

| Project   | Total | Failed | Skipped |
|-----------|-------|--------|---------|
| Modules   | 999   | 0      | 0       |
| Types     | 729   | **1**  | 0       |
| Wire      | 538   | **1**  | 0       |
| Data      | 996   | 0      | 0       |
| Generator | 203   | 0      | 0       |
| Runtime   | 805   | 0      | **6**   |

Pre-existing failures (both on the disposable stage-9 consumer-patching path,
per v6 summary — expected to be subsumed/fixed by the rebuild, not chased):

- `Data_PropertyAccess_UsesDeclaredTypeForMaterialization` (Types) — expects
  "1", gets "" via `(await aValue.Value())?.ToString()`.
- `TypedSnapshotString_NavigateEditResume_PersistsEdit` (Wire) — expects
  `seen == 2`, gets 1.

Pre-existing skips (Runtime, 6): the pinned `Stage2_ValueDoorTests` born-typed
stubs — slices 1–2 of this stage fill them:
`Value_AuthoredScalar_ReturnsTypedNumberNotRawInt`,
`VarReference_RidesAsTypedText_NeverBareCSharpString`,
`DataType_Getter_ReturnsBackingField_NoCLRSniffing`,
`TextRawValue_IsPrivate_NotPublicProperty`,
`GenericToRaw_DoesNotExist_OnItemBase`,
`RawSlot_Dissolved_BareBytesOffChannelRefineInPlace`.

## plang (`./dev.sh ptest`)

324 total, 322 pass, 0 fail, 0 timeout, 0 stale, 2 skipped (deliberate
signature-round-trip skips, pinned to signature-as-schema-wrapper).

Build: 0 errors (analyzers-off dev build; `./dev.sh full` is the pre-commit
gate).
