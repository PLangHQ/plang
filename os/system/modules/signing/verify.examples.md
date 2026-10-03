Step text: `verify %signed%, write to %ok%`
Properties: `{"Data": "%signed%"}`

Step text: `verify %data% with contracts ['C1'], on error call HandleContractError`
Properties: `{"Data": "%data%", "Contracts": ["C1"]}` — the `on error call …` is a separate on.error clause, not a property here.
