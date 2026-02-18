# v13 Summary — Remove Core/, promote to Runtime2/

Eliminated the `Core/` folder entirely. All 26 files moved to `Runtime2/` subfolders mirroring the object graph. Namespace changed from `PLang.Runtime2.Core` to `PLang.Runtime2`. Updated ~80 files across PLang, PLang.Tests, PLang.Generators, and v1 modules. All 3 projects build with 0 errors.
