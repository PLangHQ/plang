# v13 Summary — Remove Core/, promote to App/

Eliminated the `Core/` folder entirely. All 26 files moved to `App/` subfolders mirroring the object graph. Namespace changed from `App.Core` to `App`. Updated ~80 files across PLang, PLang.Tests, PLang.Generators, and v1 modules. All 3 projects build with 0 errors.
