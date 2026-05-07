# v7 Baseline (clean rebuild before any v7 change)

- C# (`dotnet run --project PLang.Tests`): **2760 / 2760 pass**, 0 fail.
- PLang (`cd Tests && plang --test`): **205 pass, 6 fail** — all 6 fails are `_fixtures_fail/*` and `_fixtures_sensitive/*.fixture.goal`, which are deliberately-failing test inputs consumed by other tests (not real failures, same shape as v6).

After v7:
- C# **2760 / 2760 pass** (no test changes).
- PLang **205 pass / 6 fixture-fails** — identical to baseline.
