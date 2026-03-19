# v2 Review Summary

Code analyzer v2 found 1 issue: `GetOrCreateDefaultAsync` didn't check `SaveAsync` result (regression from v1 which did check it). Coder v3 fixed it by throwing `InvalidOperationException` on save failure and catching in `Get.Run()`.
