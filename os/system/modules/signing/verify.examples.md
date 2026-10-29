Step text: `verify %signed%, write to %ok%`
Formal: `signing.verify(Data: item = %signed%)`

Step text: `verify %data% with contracts ['C1'], on error call HandleContractError`
Formal: `signing.verify(Data: item = %data%, Contracts: list = ["C1"])` — the `on error call …` is a separate on.error clause, not a property here.
