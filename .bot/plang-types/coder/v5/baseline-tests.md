# coder v5 — baseline tests (before edits)

Branch: `plang-types` at HEAD `a58dcfeee` (coder v4 final).

## C# (`dotnet run --project PLang.Tests`)
- 3634 / 3634 pass, 0 fail, 0 skip

## plang (`cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`)
- 248 / 248 pass

All-green baseline — no pre-existing reds. tester v4 noted one `plang`
flake on `Modules/Http/StreamCallback` (502 Bad Gateway) attributable
to external infra, not coder v4.
