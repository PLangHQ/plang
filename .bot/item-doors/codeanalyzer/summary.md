# codeanalyzer summary

**Version:** v3

**What this is:** An OBP scan of the whole item-doors diff before merge (decision 269): `git diff 1adfb9555..HEAD -- PLang PLang.Generators PlangConsole`, 431 files, weight on the code since my v2 re-check (b96c60765). Read only. Report: `v3/report.md`.

**What was done:** Three region readers plus my spot checks ran every named smell and Ingi's list (role/origin words, verb+noun, `is`/`typeof`/`ReferenceEquals`, sentinels, a second answer in `Name`, throwaway objects). v2 R3, R4, R6, R8 and R5a are closed; R1 (now in the plang format and the signature reader), R2, R5b/c/d and R7 remain. New weight: a throwaway Data made to carry a refusal (~29 unnamed Data), settings built per class to read an address, a row key packed into a Data's name, and many role-word names.

**Next:** the coder fixes the ranked findings at the root; locked-core items (typed `Bind<T>`, `Make` answering value-or-error, class answering its settings address, `item`'s json/plang virtuals) go to Ingi as proposals. I did not build or run tests.

**Code example:**
```csharp
// today: a Data made only to carry the refusal           duration/serializer/Reader.cs:22
var carrier = new data("", …); … ?? throw new FormatException(carrier.Error?.Message …);
// root: the type answers value-or-error; no carrier
```
